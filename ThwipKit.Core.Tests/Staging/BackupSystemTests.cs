using System;
using System.IO;
using System.Linq;
using Xunit;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Tests;

public class BackupSystemTests : IDisposable
{
    private readonly string _tempDir;

    public BackupSystemTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var backupSystem = new BackupSystem(_tempDir, Path.Combine(_tempDir, "backups"));
        Assert.NotNull(backupSystem);
    }

    [Fact]
    public void EnsureBackupDirectoryExists_CreatesDirectory()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        Assert.False(Directory.Exists(backupRoot));
        backupSystem.EnsureBackupDirectoryExists();
        Assert.True(Directory.Exists(backupRoot));
    }

    [Fact]
    public void TocBackupPath_ReturnsCorrectPath()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string expected = Path.Combine(backupRoot, "toc.BAK");
        Assert.Equal(expected, backupSystem.TocBackupPath);
    }

    [Fact]
    public void GetAssetBackupPath_ReturnsCorrectPath()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string expected = Path.Combine(backupRoot, "test_asset.bak");
        Assert.Equal(expected, backupSystem.GetAssetBackupPath("test_asset"));
    }

    [Fact]
    public void CreateTocBackup_CreatesBackupFile()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string tocPath = Path.Combine(_tempDir, "toc.dat");
        File.WriteAllBytes(tocPath, [0x01, 0x02, 0x03]);

        bool result = backupSystem.CreateTocBackup(tocPath);
        Assert.True(result);
        Assert.True(File.Exists(backupSystem.TocBackupPath));
    }

    [Fact]
    public void CreateTocBackup_Disabled_DoesNotCreateBackup()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot, enabled: false);

        string tocPath = Path.Combine(_tempDir, "toc.dat");
        File.WriteAllBytes(tocPath, [0x01, 0x02, 0x03]);

        bool result = backupSystem.CreateTocBackup(tocPath);
        Assert.False(result);
        Assert.False(File.Exists(backupSystem.TocBackupPath));
    }

    [Fact]
    public void CreateTocBackup_NonExistentToc_ReturnsFalse()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string tocPath = Path.Combine(_tempDir, "nonexistent_toc.dat");
        bool result = backupSystem.CreateTocBackup(tocPath);
        Assert.False(result);
    }

    [Fact]
    public void CreateAssetBackup_CreatesBackupFile()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string assetPath = Path.Combine(_tempDir, "asset.bin");
        File.WriteAllBytes(assetPath, [0x01, 0x02, 0x03]);

        bool result = backupSystem.CreateAssetBackup(assetPath, "test_asset");
        Assert.True(result);
        Assert.True(File.Exists(backupSystem.GetAssetBackupPath("test_asset")));
    }

    [Fact]
    public void RestoreToc_RestoresFromBackup()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string tocPath = Path.Combine(_tempDir, "toc.dat");
        byte[] originalContent = [0x01, 0x02, 0x03];
        File.WriteAllBytes(tocPath, originalContent);

        // Create backup
        backupSystem.CreateTocBackup(tocPath);

        // Modify original
        File.WriteAllBytes(tocPath, [0xFF, 0xFF, 0xFF]);

        // Restore
        bool result = backupSystem.RestoreToc(tocPath);
        Assert.True(result);

        byte[] restoredContent = File.ReadAllBytes(tocPath);
        Assert.Equal(originalContent, restoredContent);
    }

    [Fact]
    public void RestoreToc_NoBackup_ReturnsFalse()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string tocPath = Path.Combine(_tempDir, "toc.dat");
        bool result = backupSystem.RestoreToc(tocPath);
        Assert.False(result);
    }

    [Fact]
    public void RestoreAsset_RestoresFromBackup()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string assetPath = Path.Combine(_tempDir, "asset.bin");
        byte[] originalContent = [0x01, 0x02, 0x03];
        File.WriteAllBytes(assetPath, originalContent);

        // Create backup
        backupSystem.CreateAssetBackup(assetPath, "test_asset");

        // Modify original
        File.WriteAllBytes(assetPath, [0xFF, 0xFF, 0xFF]);

        // Restore
        bool result = backupSystem.RestoreAsset(assetPath, "test_asset");
        Assert.True(result);

        byte[] restoredContent = File.ReadAllBytes(assetPath);
        Assert.Equal(originalContent, restoredContent);
    }

    [Fact]
    public void HasTocBackup_ReturnsTrueWhenBackupExists()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string tocPath = Path.Combine(_tempDir, "toc.dat");
        File.WriteAllBytes(tocPath, [0x01, 0x02, 0x03]);
        backupSystem.CreateTocBackup(tocPath);

        Assert.True(backupSystem.HasTocBackup());
    }

    [Fact]
    public void HasTocBackup_ReturnsFalseWhenNoBackup()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        Assert.False(backupSystem.HasTocBackup());
    }

    [Fact]
    public void GetAvailableBackups_ReturnsBackupInfo()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot);

        string tocPath = Path.Combine(_tempDir, "toc.dat");
        File.WriteAllBytes(tocPath, [0x01, 0x02, 0x03]);
        backupSystem.CreateTocBackup(tocPath);

        var backups = backupSystem.GetAvailableBackups();
        Assert.Single(backups);
        Assert.Contains("toc.BAK", backups.First().FilePath);
    }

    [Fact]
    public void BackupRotation_CreatesRotatedBackups()
    {
        string backupRoot = Path.Combine(_tempDir, "backups");
        var backupSystem = new BackupSystem(_tempDir, backupRoot, maxBackups: 3);

        string tocPath = Path.Combine(_tempDir, "toc.dat");

        // Create multiple backups
        for (int i = 0; i < 5; i++)
        {
            File.WriteAllBytes(tocPath, [(byte)i]);
            backupSystem.CreateTocBackup(tocPath);
        }

        // Check that we have rotated backups
        Assert.True(File.Exists(Path.Combine(backupRoot, "toc.BAK")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "toc.1.BAK")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "toc.2.BAK")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "toc.3.BAK")));
    }
}