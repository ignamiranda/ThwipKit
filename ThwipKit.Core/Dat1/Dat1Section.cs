namespace ThwipKit.Core.Dat1;

/// <summary>
/// A single section inside a DAT1 container (the format used by the Luna/Insomniac engine
/// for both the TOC and the game's data files). Each section is addressed by a 4-byte tag and
/// carries a contiguous block of bytes at <see cref="Offset"/> within the file.
/// </summary>
public sealed class Dat1Section
{
    public uint Tag { get; }
    public uint Offset { get; }
    public uint Size { get; }
    public byte[] Data { get; }

    public Dat1Section(uint tag, uint offset, uint size, byte[] data)
    {
        Tag = tag;
        Offset = offset;
        Size = size;
        Data = data;
    }

    public string TagHex => $"0x{Tag:X8}";
}
