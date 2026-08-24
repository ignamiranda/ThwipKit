using System;
using System.Collections.Generic;
using System.IO;
using ThwipKit.Core;
using ThwipKit.Core.Hashing;
using ThwipKit.Core.Staging;
using ThwipKit.Core.Games;
using ThwipKit.Core.Sections;

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

        foreach (AssetInfo asset in assets)
        {
            if (hashTable.TryGetValue(asset.AssetIdHex, out string? resolvedName))
            {
                asset.ResolvedName = resolvedName;
            }

            asset.Type = AssetClassifier.Classify(asset);

            try
            {
                string archivePath = Path.Combine(gamePath, archiveDirectory, asset.ArchiveName);
                asset.Compression = archiveManager.GetCompressionFormat(archivePath, asset.Offset, asset.Size);
                if (File.Exists(archivePath))
                {
                    asset.LastModified = File.GetLastWriteTimeUtc(archivePath);

                    try
                    {
                        byte[] raw = archiveManager.ReadFromDsar(archivePath, asset.Offset, asset.Size);
                        asset.Crc32 = Crc32.Compute(raw);
                        asset.Crc64 = Crc64.Compute(raw);
                    }
                    catch (Exception)
                    {
                        // Asset bytes may sit in an unsupported or absent compression
                        // block; CRC stays unresolved while other metadata remains.
                    }
                }
            }
            catch (Exception)
            {
                // Archive may be absent or not a DSAR file; compression stays unresolved.
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
