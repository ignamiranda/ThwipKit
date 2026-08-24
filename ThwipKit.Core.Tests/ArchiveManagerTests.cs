using System.IO;
using ThwipKit.Core;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using Xunit;

namespace ThwipKit.Core.Tests;

public class ArchiveManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _gamePath;
    private readonly string _assetArchivePath;
    private readonly string _tocPath;
    private readonly string _archivePath;

    public ArchiveManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _gamePath = Path.Combine(_tempDir, "game");
        _assetArchivePath = Path.Combine(_gamePath, "asset_archive");
        _tocPath = Path.Combine(_assetArchivePath, "TOC");
        _archivePath = Path.Combine(_assetArchivePath, "Archive0");
        
        Directory.CreateDirectory(_assetArchivePath);
        TestFileFixtures.CreateTocFile(_tocPath);
        TestFileFixtures.CreateDsarFile(_archivePath);
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

    private static GameBase CreateTestGame()
    {
        return new ConfiguredGame(new GameDefinition
        {
            InternalId = "MSMR",
            ExecutableName = "Spider-Man Remastered.exe",
            ArchiveDirectory = "asset_archive",
            TocFileName = "TOC",
            TocFormat = TocFormat.ZlibDat1,
            CompressionFormats = [CompressionFormat.Lz4],
            SectionTags = new Dictionary<string, string>
            {
                ["ArchivesMap"] = "F0BF8A39",
                ["AssetIDs"] = "8A7B6D50",
                ["SizeEntries"] = "61F4BC65",
                ["Offsets"] = "B520D7DC"
            }
        });
    }

    [Fact]
    public void GetTextureNamesReturnsExpectedNames()
    {
        var manager = new ArchiveManager(CreateTestGame());
        var names = manager.GetTextureNames(_gamePath);
        
        Assert.Contains("0x1122334455667788", names);
    }

    [Fact]
    public void ExtractTextureToPngReturnsTrueForValidTexture()
    {
        var manager = new ArchiveManager(CreateTestGame());
        string pngPath = Path.Combine(_tempDir, "output.png");
        
        bool result = manager.ExtractTextureToPng(_gamePath, "0x1122334455667788", pngPath);
        
        Assert.True(result);
        Assert.True(File.Exists(pngPath));
    }

    [Fact]
    public void RebuildTextureFromPngReturnsTrueForValidTexture()
    {
        var manager = new ArchiveManager(CreateTestGame());
        string pngPath = Path.Combine(_tempDir, "input.png");
        File.WriteAllBytes(pngPath, new byte[100]);
        
        bool result = manager.RebuildTextureFromPng(_gamePath, "0x1122334455667788", pngPath);
        
        Assert.True(result);
    }

    [Fact]
    public void RestoreTextureFromBackupReturnsTrueWhenBackupExists()
    {
        var manager = new ArchiveManager(CreateTestGame());
        string backupDir = Path.Combine(_gamePath, "asset_archive", "backups");
        Directory.CreateDirectory(backupDir);
        string backupPath = Path.Combine(backupDir, "0x1122334455667788_20240101_000000.texture.bak");
        File.WriteAllBytes(backupPath, new byte[123]);
        
        bool result = manager.RestoreTextureFromBackup(_gamePath, "0x1122334455667788");
        
        Assert.True(result);
    }
}