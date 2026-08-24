using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ThwipKit.Core.Staging;

public sealed class ProjectValidator
{
    private readonly ProjectManager _manager;
    private readonly StageManager _stageManager;

    public ProjectValidator(ProjectManager manager, StageManager stageManager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _stageManager = stageManager ?? throw new ArgumentNullException(nameof(stageManager));
    }

    public sealed class AssetValidationResult
    {
        public ulong AssetId { get; set; }
        public string? ResolvedName { get; set; }
        public TrackedAssetStatus Status { get; set; }
        public string? ExpectedHash { get; set; }
        public string? ActualHash { get; set; }
        public bool IsValid { get; set; }
        public string? Note { get; set; }
    }

    public IReadOnlyList<AssetValidationResult> Validate()
    {
        var results = new List<AssetValidationResult>();

        foreach (TrackedAsset asset in _manager.GetTrackedAssets())
        {
            string absolutePath = ((IProjectTracker)_manager).GetStageAbsolutePath(asset);

            var result = new AssetValidationResult
            {
                AssetId = asset.AssetId,
                ResolvedName = asset.ResolvedName,
                Status = asset.Status,
                ExpectedHash = asset.ValidationHash
            };

            if (!File.Exists(absolutePath))
            {
                result.Status = TrackedAssetStatus.Missing;
                result.IsValid = false;
                result.Note = "Staged file missing";
            }
            else
            {
                byte[] bytes = File.ReadAllBytes(absolutePath);
                result.ActualHash = Sha256Hex(bytes);

                if (asset.Status == TrackedAssetStatus.Modified)
                {
                    result.Status = TrackedAssetStatus.Modified;
                    result.IsValid = string.Equals(result.ExpectedHash, result.ActualHash, StringComparison.OrdinalIgnoreCase);
                    result.Note = result.IsValid
                        ? "Modified asset matches recorded hash"
                        : "Modified asset hash mismatch";
                }
                else if (!string.IsNullOrEmpty(asset.ValidationHash)
                         && !string.Equals(asset.ValidationHash, result.ActualHash, StringComparison.OrdinalIgnoreCase))
                {
                    result.Status = TrackedAssetStatus.Modified;
                    result.IsValid = false;
                    result.Note = "Staged file hash does not match recorded hash";
                }
                else
                {
                    result.Status = TrackedAssetStatus.Extracted;
                    result.IsValid = true;
                    result.Note = asset.ValidationHash is null
                        ? "No recorded hash to compare"
                        : "Asset matches recorded hash";
                }
            }

            results.Add(result);
        }

        return results;
    }

    public bool IsProjectValid() => Validate().All(r => r.IsValid);

    private static string Sha256Hex(byte[] data)
    {
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);
    }
}