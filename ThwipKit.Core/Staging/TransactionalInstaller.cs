using System;
using System.Collections.Generic;
using System.IO;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Staging;

public sealed class TransactionalInstaller
{
    private readonly BackupSystem _backupSystem;
    private readonly ConflictDetector _conflictDetector;
    private readonly StageManager _stageManager;
    private readonly string _gamePath;

    public TransactionalInstaller(
        BackupSystem backupSystem,
        ConflictDetector conflictDetector,
        StageManager stageManager,
        string gamePath)
    {
        _backupSystem = backupSystem ?? throw new ArgumentNullException(nameof(backupSystem));
        _conflictDetector = conflictDetector ?? throw new ArgumentNullException(nameof(conflictDetector));
        _stageManager = stageManager ?? throw new ArgumentNullException(nameof(stageManager));
        _gamePath = gamePath ?? throw new ArgumentNullException(nameof(gamePath));
    }

    public InstallationResult InstallMods(IEnumerable<ModInfo> mods, bool createBackup = true)
    {
        var result = new InstallationResult
        {
            StartTime = DateTime.UtcNow,
            ModsCount = mods is null ? 0 : mods.Count()
        };

        if (mods == null || !mods.Any())
        {
            result.Success = true;
            result.Message = "No mods to install";
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        try
        {
            // Step 1: Create backup
            if (createBackup)
            {
                string tocPath = Path.Combine(_gamePath, "data0", "toc.dat");
                if (!_backupSystem.CreateTocBackup(tocPath))
                {
                    result.Warnings.Add("Failed to create TOC backup");
                }
            }

            // Step 2: Detect conflicts
            ConflictDetectionResult conflictResult = _conflictDetector.DetectConflicts(_gamePath, mods);
            if (conflictResult.HasConflicts)
            {
                result.ConflictsDetected = true;
                foreach (var conflict in conflictResult.ConflictingAssets)
                {
                    result.ConflictDetails.Add($"Asset {conflict.AssetPath} conflicts between: {string.Join(", ", conflict.ConflictingMods)}");
                }

                // For now, we'll skip conflicting mods
                // In a real implementation, we'd have user interaction or auto-resolution
                foreach (var conflict in conflictResult.ConflictingAssets)
                {
                    result.SkippedMods.AddRange(conflict.ConflictingMods);
                }
            }

            // Step 3: Apply mods in order
            int appliedCount = 0;
            foreach (ModInfo mod in mods)
            {
                if (result.SkippedMods.Contains(mod.Name))
                {
                    result.SkippedMods.Add(mod.Name);
                    continue;
                }

                if (ApplyMod(mod))
                {
                    result.AppliedMods.Add(mod.Name);
                    appliedCount++;
                }
                else
                {
                    result.FailedMods.Add(mod.Name);
                }
            }

            result.Success = appliedCount > 0 && result.FailedMods.Count == 0;
            result.Message = result.Success
                ? $"Successfully installed {appliedCount} mods"
                : $"Installed {appliedCount} mods with {result.FailedMods.Count} failures";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Installation failed: {ex.Message}";
            result.Error = ex;

            // Attempt rollback
            if (createBackup && _backupSystem.HasTocBackup())
            {
                string tocPath = Path.Combine(_gamePath, "data0", "toc.dat");
                if (_backupSystem.RestoreToc(tocPath))
                {
                    result.RolledBack = true;
                    result.RollbackMessage = "Successfully rolled back to backup";
                }
                else
                {
                    result.RollbackMessage = "Failed to rollback";
                }
            }
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    private bool ApplyMod(ModInfo mod)
    {
        // Placeholder - actual implementation would:
        // 1. Extract mod contents
        // 2. Copy files to appropriate locations
        // 3. Update TOC if needed
        // 4. Validate the installation

        // For now, just return true to indicate success
        return true;
    }

    public bool Rollback()
    {
        string tocPath = Path.Combine(_gamePath, "data0", "toc.dat");
        if (_backupSystem.HasTocBackup())
        {
            return _backupSystem.RestoreToc(tocPath);
        }
        return false;
    }
}

public sealed class InstallationResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ModsCount { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool ConflictsDetected { get; set; }
    public bool RolledBack { get; set; }
    public string RollbackMessage { get; set; } = string.Empty;
    public Exception? Error { get; set; }
    public List<string> AppliedMods { get; } = [];
    public List<string> FailedMods { get; } = [];
    public List<string> SkippedMods { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> ConflictDetails { get; } = [];
}
