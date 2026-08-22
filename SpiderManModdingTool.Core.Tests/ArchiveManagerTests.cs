using System;
using System.IO;
using System.IO.Compression;
using SpiderManModdingTool.Core;
using SpiderManModdingTool.Core.GameDefinitions;
using SpiderManModdingTool.Core.Games;
using Xunit;

namespace SpiderManModdingTool.Core.Tests;

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
        CreateTocFile();
        CreateDsarFile();
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
            TocFormat = "ZlibDat1",
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

    private void CreateTocFile()
    {
        var sections = new[]
        {
            (Tag: new byte[] { 0xF0, 0xBF, 0x8A, 0x39 }, Data: CreateArchiveEntry("Archive0")),
            (Tag: new byte[] { 0x8A, 0x7B, 0x6D, 0x50 }, Data: Write(writer => writer.Write(0x1122334455667788UL))),
            (Tag: new byte[] { 0x61, 0xF4, 0xBC, 0x65 }, Data: Write(writer => { writer.Write(1U); writer.Write(123U); writer.Write(0U); })),
            (Tag: new byte[] { 0xB5, 0x20, 0xD7, 0xDC }, Data: Write(writer => { writer.Write(0U); writer.Write(456U); }))
        };

        byte[] dat1;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            int headerLength = 16 + (sections.Length * 12) + "ArchiveTOC".Length + 1;
            int dataOffset = (headerLength + 15) & ~15;
            writer.Write(new byte[] { 0x31, 0x54, 0x41, 0x44 });
            writer.Write(0U);
            writer.Write((uint)(dataOffset + sections.Sum(section => section.Data.Length)));
            writer.Write((ushort)sections.Length);
            writer.Write((ushort)0);
            int offset = dataOffset;
            foreach (var section in sections)
            {
                writer.Write(section.Tag);
                writer.Write((uint)offset);
                writer.Write((uint)section.Data.Length);
                offset += section.Data.Length;
            }
            writer.Write(System.Text.Encoding.ASCII.GetBytes("ArchiveTOC"));
            writer.Write((byte)0);
            writer.Write(new byte[dataOffset - stream.Position]);
            foreach (var section in sections)
            {
                writer.Write(section.Data);
            }
            dat1 = stream.ToArray();
        }

        using var file = File.Create(_tocPath);
        using var outerWriter = new BinaryWriter(file, System.Text.Encoding.UTF8, true);
        outerWriter.Write(new byte[] { 0xAF, 0x12, 0xAF, 0x77 });
        outerWriter.Write((uint)dat1.Length);
        using var zlib = new ZLibStream(file, CompressionLevel.SmallestSize, true);
        zlib.Write(dat1);
    }

    private void CreateDsarFile()
    {
        using var file = File.Create(_archivePath);
        using var writer = new BinaryWriter(file, System.Text.Encoding.UTF8, true);
        writer.Write(new byte[] { (byte)'D', (byte)'S', (byte)'A', (byte)'R' });
        writer.Write(1U);
        writer.Write(1U);
        writer.Write(0U);
        writer.Write(0UL);
        writer.Write(new byte[8]);
        
        writer.Write(0U);
        writer.Write(0U);
        writer.Write(0U);
        writer.Write(0U);
        writer.Write(123U);
        writer.Write(100U);
        writer.Write((byte)3);
        writer.Write(new byte[7]);
        
        writer.Write(new byte[100]);
    }

    private static byte[] CreateArchiveEntry(string name)
    {
        return Write(writer =>
        {
            writer.Write(1U);
            writer.Write(2U);
            byte[] nameBytes = new byte[64];
            System.Text.Encoding.ASCII.GetBytes(name).CopyTo(nameBytes, 0);
            writer.Write(nameBytes);
        });
    }

    private static byte[] Write(Action<BinaryWriter> action)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        action(writer);
        return stream.ToArray();
    }
}