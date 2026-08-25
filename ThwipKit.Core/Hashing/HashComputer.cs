using System.Text;

namespace ThwipKit.Core.Hashing;

/// <summary>
/// Computes the asset hash the game stores in its TOC: CRC-64 of the normalized path,
/// then a final transform <c>(value &gt;&gt; 2) | 0x8000000000000000</c>. This mirrors the
/// Luna engine's <c>DAT1.CRC64.Hash</c> used by Overstrike/ALERT.
/// </summary>
public static class HashComputer
{
    private const ulong TopBit = 0x8000000000000000UL;

    /// <summary>Computes the 64-bit asset id for a normalized asset path.</summary>
    public static ulong ComputeAssetId(string path)
    {
        string normalized = HashNormalizer.Normalize(path);
        byte[] bytes = Encoding.UTF8.GetBytes(normalized);
        ulong value = InsomniacCrc64.Compute(bytes, InsomniacCrc64.InitialValue);
        return (value >> 2) | TopBit;
    }

    /// <summary>Formats an asset id the same way <c>AssetInfo.AssetIdHex</c> does, for dictionary keys.</summary>
    public static string ToAssetIdHex(ulong assetId) => $"0x{assetId:X16}";
}
