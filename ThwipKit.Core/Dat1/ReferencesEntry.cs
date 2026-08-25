namespace ThwipKit.Core.Dat1;

/// <summary>
/// One record of a DAT1 <c>ReferencesSection</c>. The Luna engine stores asset references as
/// (AssetId, StringOffset, ExtensionHash) triples. <see cref="AssetId"/> is the asset id already
/// transformed by <c>0x8000000000000000 | (CRC64(normalize(path)) &gt;&gt; 2)</c> and is directly
/// comparable to the ids stored in the TOC. <see cref="StringOffset"/> is an absolute byte offset
/// into the owning DAT1 file where the null-terminated asset path string lives.
/// </summary>
public sealed class ReferencesEntry
{
    public ulong AssetId { get; init; }
    public uint StringOffset { get; init; }
    public uint ExtensionHash { get; init; }
}
