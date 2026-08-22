namespace SpiderManModdingTool.Core.Assets;

public sealed class AssetInfo
{
    public ulong AssetId { get; init; }
    public string AssetIdHex => $"0x{AssetId:X16}";
    public uint Size { get; init; }
    public uint Offset { get; init; }
    public string ArchiveName { get; init; } = string.Empty;
    public uint ArchiveIndex { get; init; }
    public string? ResolvedName { get; set; }
}
