using System.IO;

namespace ThwipKit.Core.Dat1;

/// <summary>
/// Parses the 16-byte records of a DAT1 <c>ReferencesSection</c>. The canonical reference section
/// tag for configuration asset references is <see cref="Tag"/>, but every asset type ships a
/// references section with the same layout, so callers typically use
/// <see cref="Dat1Container.FindReferencesSections"/> (which auto-discovers by structure) instead
/// of matching a single tag.
/// </summary>
public static class ReferencesSection
{
    public const uint Tag = 0x58B8558A;
    private const int RecordSize = 16;

    /// <summary>
    /// Parses <paramref name="data"/> as a references section. Returns <see langword="null"/> when
    /// the length is not a multiple of 16 bytes.
    /// </summary>
    public static List<ReferencesEntry>? TryParse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < RecordSize || data.Length % RecordSize != 0)
        {
            return null;
        }

        var entries = new List<ReferencesEntry>(data.Length / RecordSize);
        using var reader = new BinaryReader(new MemoryStream(data));
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            ulong assetId = reader.ReadUInt64();
            uint stringOffset = reader.ReadUInt32();
            uint extensionHash = reader.ReadUInt32();
            entries.Add(new ReferencesEntry
            {
                AssetId = assetId,
                StringOffset = stringOffset,
                ExtensionHash = extensionHash
            });
        }

        return entries;
    }
}
