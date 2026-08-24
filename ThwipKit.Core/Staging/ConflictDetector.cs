using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThwipKit.Core.Assets;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Staging;

public sealed class ConflictDetector
{
    private readonly StageManager _stageManager;
    private readonly AssetBrowser _assetBrowser;

    public ConflictDetector(StageManager stageManager, AssetBrowser assetBrowser)
    {
        _stageManager = stageManager ?? throw new ArgumentNullException(nameof(stageManager));
        _assetBrowser = assetBrowser ?? throw new ArgumentNullException(nameof(assetBrowser));
    }

    public ConflictDetectionResult DetectConflicts(string gamePath, IEnumerable<ModInfo> modsToInstall)
    {
        var result = new ConflictDetectionResult();
        var stagedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Collect all assets from staged mods
            foreach (ModInfo mod in modsToInstall)
            {
                foreach (string assetPath in mod.GetModifiedAssets())
                {
                    string relativePath = GetRelativeAssetPath(assetPath);
                    if (stagedAssets.Contains(relativePath))
                    {
                        // This asset appears in multiple mods
                        var existing = result.ConflictingAssets
                            .FirstOrDefault(c => c.AssetPath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.ConflictingMods.Add(mod.Name);
                        }
                        else
                        {
                            result.ConflictingAssets.Add(new ConflictingAsset
                            {
                                AssetPath = relativePath,
                                ConflictingMods = new List<string> { mod.Name }
                            });
                        }
                    }
                    else
                    {
                        stagedAssets.Add(relativePath);
                    }
                }
            }

        // Check for conflicts with existing game files
        foreach (string assetPath in stagedAssets)
        {
            string relativePath = GetRelativeAssetPath(assetPath);
            AssetInfo? existingAsset = _assetBrowser.GetAllAssets(gamePath)
                .FirstOrDefault(a => a.ResolvedName?.Equals(relativePath, StringComparison.OrdinalIgnoreCase) ?? false);

            if (existingAsset != null)
            {
                result.ExistingAssets.Add(new ExistingAssetConflict
                {
                    AssetPath = relativePath,
                    AssetId = existingAsset.AssetId,
                    AssetIdHex = existingAsset.AssetIdHex
                });
            }
        }

        return result;
    }

    public ConflictResolution ChooseWinner(ConflictingAsset conflict, string winningModName)
    {
        return new ConflictResolution
        {
            AssetPath = conflict.AssetPath,
            ResolutionType = ResolutionType.UseMod,
            WinningMod = winningModName,
            Resolved = true
        };
    }

    public ConflictResolution SkipAsset(ConflictingAsset conflict)
    {
        return new ConflictResolution
        {
            AssetPath = conflict.AssetPath,
            ResolutionType = ResolutionType.Skip,
            Resolved = true
        };
    }

    private static string GetRelativeAssetPath(string fullPath)
    {
        // Simplified - extract relative path from full path
        // In real implementation, this would use proper path manipulation
        return Path.GetFileName(fullPath);
    }
}

public sealed class ConflictDetectionResult
{
    public List<ConflictingAsset> ConflictingAssets { get; } = [];
    public List<ExistingAssetConflict> ExistingAssets { get; } = [];
    public List<ConflictResolution> Resolutions { get; } = [];

    public bool HasConflicts => ConflictingAssets.Count > 0 || ExistingAssets.Count > 0;
    public bool HasUnresolvedConflicts => HasConflicts && Resolutions.Count < ConflictingAssets.Count + ExistingAssets.Count;
}

public sealed class ConflictingAsset
{
    public required string AssetPath { get; set; }
    public List<string> ConflictingMods { get; set; } = [];
}

public sealed class ExistingAssetConflict
{
    public required string AssetPath { get; set; }
    public ulong AssetId { get; set; }
    public string AssetIdHex { get; set; } = string.Empty;
}

public enum ResolutionType
{
    UseMod,
    Skip,
    Merge
}

public sealed class ConflictResolution
{
    public required string AssetPath { get; set; }
    public ResolutionType ResolutionType { get; set; }
    public string? WinningMod { get; set; }
    public bool Resolved { get; set; }
}

public sealed class ModInfo
{
    public required string Name { get; set; }
    public required string Path { get; set; }

    public IEnumerable<string> GetModifiedAssets()
    {
        // Placeholder - actual implementation would parse mod file
        return [];
    }
}
