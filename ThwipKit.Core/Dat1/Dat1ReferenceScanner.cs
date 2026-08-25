using System;
using ThwipKit.Core.Dat1;

namespace ThwipKit.Core.Dat1;

/// <summary>
/// Scans a decompressed asset byte array for DAT1 reference sections and returns the
/// resolved (AssetId, Path) pairs, if the bytes form a valid DAT1 container.
/// </summary>
public static class Dat1ReferenceScanner
{
    /// <summary>
    /// Result of scanning one asset's bytes for DAT1 reference sections.
    /// <see cref="IsDat1"/> is true only when the bytes form a structurally valid DAT1 container
    /// (not merely a matching magic); <see cref="References"/> is then the resolvable reference list.
    /// </summary>
    public sealed class Dat1ScanResult
    {
        public bool IsDat1 { get; init; }
        public IReadOnlyList<(ulong AssetId, string Path)> References { get; init; } = [];
    }

    private const uint Dat1Magic = 0x44415431;

    /// <summary>
    /// Scans <paramref name="assetData"/> for DAT1 reference sections.
    /// Returns a result where <see cref="Dat1ScanResult.IsDat1"/> indicates whether the bytes
    /// are a structurally valid DAT1 container, and <see cref="Dat1ScanResult.References"/> contains
    /// the resolved (asset id, path) pairs from any reference sections found.
    /// </summary>
    public static Dat1ScanResult Scan(byte[] assetData)
    {
        if (assetData is null || assetData.Length < 4 ||
            BitConverter.ToUInt32(assetData, 0) != Dat1Magic)
        {
            return new Dat1ScanResult { IsDat1 = false };
        }

        Dat1Container container;
        try
        {
            container = Dat1Container.Parse(assetData);
        }
        catch (InvalidDataException)
        {
            return new Dat1ScanResult { IsDat1 = false };
        }

        var references = new List<(ulong, string)>();
        foreach (var (_, entries) in container.FindReferencesSections())
        {
            foreach (var entry in entries)
            {
                string? path = container.GetStringAt(entry.StringOffset);
                if (!string.IsNullOrEmpty(path))
                    references.Add((entry.AssetId, path!));
            }
        }

        return new Dat1ScanResult { IsDat1 = true, References = references };
    }
}