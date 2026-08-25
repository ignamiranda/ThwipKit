using System.Text;

namespace ThwipKit.Core.Dat1;

/// <summary>
/// Parses a Luna-engine DAT1 container and exposes its sections. A DAT1 file is a small header
/// followed by a table of section descriptors (tag, offset, size) and then the section payloads.
/// The bytes between the header table and the first section form a null-terminated string table;
/// <see cref="GetStringAt"/> reads a path string from an absolute file offset (which is how
/// <see cref="ReferencesEntry.StringOffset"/> addresses asset paths).
/// </summary>
public sealed class Dat1Container
{
    private const uint Magic = 0x44415431; // "DAT1"

    private readonly byte[] _data;

    public uint Unk1 { get; }
    public uint DeclaredSize { get; }
    public IReadOnlyList<Dat1Section> Sections { get; }

    private Dat1Container(byte[] data, uint unk1, uint declaredSize, IReadOnlyList<Dat1Section> sections)
    {
        _data = data;
        Unk1 = unk1;
        DeclaredSize = declaredSize;
        Sections = sections;
    }

    public static Dat1Container Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 16)
        {
            throw new InvalidDataException("DAT1 stream is too small to contain a header.");
        }

        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        if (reader.ReadUInt32() != Magic)
        {
            throw new InvalidDataException("Stream does not begin with the DAT1 magic.");
        }

        uint unk1 = reader.ReadUInt32();
        uint declaredSize = reader.ReadUInt32();
        if (declaredSize < 16 || declaredSize > data.Length)
        {
            throw new InvalidDataException($"DAT1 declared size {declaredSize} is outside the {data.Length}-byte stream.");
        }

        ushort sectionCount = reader.ReadUInt16();
        ushort unknownCount = reader.ReadUInt16();

        long tableLength = (long)sectionCount * 12 + (long)unknownCount * 8;
        if (reader.BaseStream.Position + tableLength > declaredSize)
        {
            throw new InvalidDataException("DAT1 section table exceeds the declared size.");
        }

        var descriptors = new List<(uint Tag, uint Offset, uint Size)>(sectionCount);
        for (int i = 0; i < sectionCount; i++)
        {
            descriptors.Add((reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32()));
        }

        reader.ReadBytes(unknownCount * 8);

        var sections = new List<Dat1Section>(sectionCount);
        foreach (var (tag, offset, size) in descriptors)
        {
            if ((ulong)offset + size > declaredSize || (ulong)offset + size > (ulong)data.Length)
            {
                throw new InvalidDataException($"DAT1 section {tag:X8} [{offset}, {offset + size}) is out of bounds.");
            }

            var sectionData = new byte[size];
            Array.Copy(data, offset, sectionData, 0, (int)size);
            sections.Add(new Dat1Section(tag, offset, size, sectionData));
        }

        return new Dat1Container(data, unk1, declaredSize, sections);
    }

    /// <summary>
    /// Reads a null-terminated UTF-8 string beginning at <paramref name="absoluteOffset"/> within
    /// the source file. Returns <see langword="null"/> when the offset is out of range, empty, or
    /// contains non-printable bytes.
    /// </summary>
    public string? GetStringAt(long absoluteOffset)
    {
        if (absoluteOffset < 0 || absoluteOffset >= _data.Length)
        {
            return null;
        }

        int start = (int)absoluteOffset;
        int end = start;
        while (end < _data.Length && _data[end] != 0)
        {
            end++;
        }

        if (end == start)
        {
            return null;
        }

        ReadOnlySpan<byte> span = _data.AsSpan(start, end - start);
        foreach (byte b in span)
        {
            if (b < 0x20 || b == 0x7F)
            {
                return null;
            }
        }

        return Encoding.UTF8.GetString(span);
    }

    /// <summary>
    /// Scans every section and returns the ones that look like <c>ReferencesSection</c> payloads:
    /// 16-byte-aligned records whose <see cref="ReferencesEntry.StringOffset"/> resolves to a
    /// non-empty, path-like string. This auto-discovers reference sections without depending on a
    /// hard-coded tag list (multiple asset types ship their own references section).
    /// </summary>
    public IReadOnlyList<(Dat1Section Section, IReadOnlyList<ReferencesEntry> Entries)> FindReferencesSections()
    {
        var result = new List<(Dat1Section, IReadOnlyList<ReferencesEntry>)>();
        foreach (var section in Sections)
        {
            if (section.Size < 16 || section.Size % 16 != 0)
            {
                continue;
            }

            var entries = ReferencesSection.TryParse(section.Data);
            if (entries is null || entries.Count == 0)
            {
                continue;
            }

            int valid = 0;
            foreach (var entry in entries)
            {
                string? path = GetStringAt(entry.StringOffset);
                if (!string.IsNullOrEmpty(path) && path!.IndexOf('/') >= 0)
                {
                    valid++;
                }
            }

            if (valid * 2 >= entries.Count)
            {
                result.Add((section, entries));
            }
        }

        return result;
    }
}
