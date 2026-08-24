using System;
using System.Collections.Generic;
using System.IO;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Staging;

public sealed class BackupSystem
{
    private readonly string _gamePath;
    private readonly string _backupRoot;
    private readonly int _maxBackups;
    private readonly bool _enabled;

    public BackupSystem(string gamePath, string backupRoot, int maxBackups = 10, bool enabled = true)
    {
        _gamePath = gamePath ?? throw new ArgumentNullException(nameof(gamePath));
        _backupRoot = Path.GetFullPath(backupRoot ?? throw new ArgumentNullException(nameof(backupRoot)));
        _maxBackups = Math.Max(1, maxBackups);
        _enabled = enabled;
    }

    public string TocBackupPath => Path.Combine(_backupRoot, "toc.BAK");

    public string GetAssetBackupPath(string assetName) => Path.Combine(_backupRoot, $"{assetName}.bak");

    public void EnsureBackupDirectoryExists()
    {
        if (!Directory.Exists(_backupRoot))
        {
            Directory.CreateDirectory(_backupRoot);
        }
    }

    public bool CreateTocBackup(string tocPath)
    {
        if (!_enabled)
        {
            return false;
        }

        EnsureBackupDirectoryExists();

        if (!File.Exists(tocPath))
        {
            return false;
        }

        string backupPath = TocBackupPath;
        if (File.Exists(backupPath))
        {
            // Rotate existing backups
            RotateBackups(backupPath);
        }

        try
        {
            File.Copy(tocPath, backupPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool CreateAssetBackup(string assetPath, string backupName)
    {
        if (!_enabled)
        {
            return false;
        }

        EnsureBackupDirectoryExists();

        if (!File.Exists(assetPath))
        {
            return false;
        }

        string backupPath = GetAssetBackupPath(backupName);
        if (File.Exists(backupPath))
        {
            RotateBackups(backupPath);
        }

        try
        {
            File.Copy(assetPath, backupPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool RestoreToc(string tocPath)
    {
        string backupPath = TocBackupPath;
        if (!File.Exists(backupPath))
        {
            return false;
        }

        try
        {
            if (File.Exists(tocPath))
            {
                File.Delete(tocPath);
            }
            File.Copy(backupPath, tocPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool RestoreAsset(string targetPath, string backupName)
    {
        string backupPath = GetAssetBackupPath(backupName);
        if (!File.Exists(backupPath))
        {
            return false;
        }

        try
        {
            EnsureDirectoryForFile(targetPath);
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Copy(backupPath, targetPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureDirectoryForFile(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void RotateBackups(string primaryBackupPath)
    {
        string? directory = Path.GetDirectoryName(primaryBackupPath);
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(primaryBackupPath);
        string ext = Path.GetExtension(primaryBackupPath);

        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        // Find all rotated backups
        var backups = new List<string>();
        for (int i = 1; i <= _maxBackups; i++)
        {
            string rotatedPath = Path.Combine(directory, $"{fileNameWithoutExt}.{i}{ext}");
            if (File.Exists(rotatedPath))
            {
                backups.Add(rotatedPath);
            }
        }

        // Shift backups: .bak.2 -> .bak.3, .bak.1 -> .bak.2, .bak -> .bak.1
        for (int i = backups.Count - 1; i >= 0; i--)
        {
            int nextIndex = i + 2;
            if (nextIndex > _maxBackups)
            {
                // Remove oldest
                try { File.Delete(backups[i]); } catch { }
            }
            else
            {
                string newPath = Path.Combine(directory, $"{fileNameWithoutExt}.{nextIndex}{ext}");
                try { File.Move(backups[i], newPath, overwrite: true); } catch { }
            }
        }

        // Move primary to .1
        string firstRotation = Path.Combine(directory, $"{fileNameWithoutExt}.1{ext}");
        try { File.Move(primaryBackupPath, firstRotation, overwrite: true); } catch { }
    }

    public IEnumerable<BackupInfo> GetAvailableBackups()
    {
        if (!Directory.Exists(_backupRoot))
        {
            return [];
        }

        var backups = new List<BackupInfo>();
        foreach (string file in Directory.GetFiles(_backupRoot, "*.BAK", SearchOption.TopDirectoryOnly))
        {
            backups.Add(new BackupInfo
            {
                FilePath = file,
                Timestamp = File.GetLastWriteTime(file),
                Size = new FileInfo(file).Length
            });
        }

        return backups.OrderByDescending(b => b.Timestamp);
    }

    public bool HasTocBackup() => File.Exists(TocBackupPath);
}

public sealed class BackupInfo
{
    public required string FilePath { get; set; }
    public DateTime Timestamp { get; set; }
    public long Size { get; set; }
}
