using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThwipKit.Core;
using ThwipKit.Core.Hashing;
using ThwipKit.Core.Staging;
using ThwipKit.Core.Games;
using ThwipKit.Core.Sections;
using ThwipKit.Core.Dat1;

namespace ThwipKit.Core.Assets;

public class AssetCatalog
{
    private readonly GameBase _game;

    public AssetCatalog(GameBase game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
    }

    public IReadOnlyList<AssetInfo> GetAssets(string gamePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gamePath);

        string tocPath = GetTocPath(gamePath);
        if (!File.Exists(tocPath))
        {
            throw new FileNotFoundException("TOC file not found", tocPath);
        }

        TocData toc = _game.ParseToc(tocPath);
        IReadOnlyDictionary<string, string> hashTable = _game.LoadHashTable(gamePath);
        IReadOnlyList<AssetInfo> assets = BuildAssets(toc);

        var archiveManager = new ArchiveManager(_game);
        string archiveDirectory = _game.ArchiveDirectory;

        // Edges collected during the per-asset scan: (referrerId, referencedId).
        var edges = new List<(ulong ReferrerId, ulong ReferencedId)>();
        bool anyDataRead = false;

        foreach (AssetInfo asset in assets)
        {
            if (hashTable.TryGetValue(asset.AssetIdHex, out string? resolvedName))
            {
                asset.ResolvedName = resolvedName;
            }

            asset.Type = AssetClassifier.Classify(asset);
            asset.IsInternalTarget = _game.Definition.IsInternalTarget;

            try
            {
                string archivePath = Path.Combine(gamePath, archiveDirectory, asset.ArchiveName);
                asset.Compression = archiveManager.GetCompressionFormat(archivePath, asset.Offset, asset.Size);
                if (File.Exists(archivePath))
                {
                    asset.LastModified = File.GetLastWriteTimeUtc(archivePath);
                    byte[]? assetData = archiveManager.GetAssetData(archivePath, asset.Offset, asset.Size);
                    if (assetData is not null)
                    {
                        anyDataRead = true;
                        asset.Crc32 = Crc32.Compute(assetData);
                        asset.Crc64 = Crc64.Compute(assetData);

                        Dat1ReferenceScanner.Dat1ScanResult scan = Dat1ReferenceScanner.Scan(assetData);
                        if (scan.IsDat1)
                        {
                            asset.Dependencies = scan.References
                                .Select(pair => pair.Path)
                                .ToList();
                            foreach (var (referencedId, _) in scan.References)
                            {
                                edges.Add((asset.AssetId, referencedId));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Archive may be absent or not a DSAR file; compression stays unresolved.
            }
        }

        // Inbound index: referencedId -> (distinct referrer ids -> their display string, total reference entries).
        // Referrers are deduplicated by asset id, not display string, so two distinct referrers whose
        // hashes.txt entries resolve to the same path remain distinct.
        var inbound = new Dictionary<ulong, (Dictionary<ulong, string> Referrers, uint Count)>();
        foreach (var (referrerId, referencedId) in edges)
        {
            string referrerDisplay = hashTable.TryGetValue(HashComputer.ToAssetIdHex(referrerId), out string? referrerName)
                ? referrerName!
                : HashComputer.ToAssetIdHex(referrerId);
            if (!inbound.TryGetValue(referencedId, out var bucket))
            {
                bucket = (new Dictionary<ulong, string>(), 0);
            }
            bucket.Referrers[referrerId] = referrerDisplay;
            bucket.Count++;
            inbound[referencedId] = bucket;
        }

        if (anyDataRead)
        {
            foreach (AssetInfo asset in assets)
            {
                if (inbound.TryGetValue(asset.AssetId, out var bucket))
                {
                    asset.References = bucket.Referrers
                        .OrderBy(pair => pair.Value, StringComparer.Ordinal)
                        .ThenBy(pair => pair.Key)
                        .Select(pair => pair.Value)
                        .ToList();
                    asset.UsageCount = bucket.Count;
                }
                else
                {
                    asset.References = [];
                    asset.UsageCount = 0;
                }
            }
        }

        return assets;
    }

    public IReadOnlyList<AssetInfo> BuildAssets(TocData toc)
    {
        ArgumentNullException.ThrowIfNull(toc);

        var assets = new List<AssetInfo>();

        for (int i = 0; i < toc.SizeEntries.Count; i++)
        {
            SizeEntriesSection sizeEntry = toc.SizeEntries[i];

            if (sizeEntry.Index >= toc.AssetIds.Count)
            {
                throw new InvalidDataException($"Size entry at position {i} references asset ID index {sizeEntry.Index}, but only {toc.AssetIds.Count} asset IDs exist.");
            }

            if (i >= toc.Offsets.Count)
            {
                throw new InvalidDataException($"Size entry at position {i} has no matching offset entry at position {i}.");
            }

            OffsetsSection offsetEntry = toc.Offsets[i];
            if (offsetEntry.ArchiveIndex >= toc.Archives.Count)
            {
                throw new InvalidDataException($"Offset entry at position {i} references archive index {offsetEntry.ArchiveIndex}, but only {toc.Archives.Count} archives exist.");
            }

            assets.Add(new AssetInfo
            {
                AssetId = toc.AssetIds[(int)sizeEntry.Index],
                Size = sizeEntry.Value,
                Offset = offsetEntry.OffsetInArchive,
                ArchiveName = toc.Archives[(int)offsetEntry.ArchiveIndex].Name,
                ArchiveIndex = offsetEntry.ArchiveIndex,
            });
        }

        return assets;
    }

    private string GetTocPath(string gamePath) =>
        Path.Combine(gamePath, _game.ArchiveDirectory, _game.TocFileName);
}
