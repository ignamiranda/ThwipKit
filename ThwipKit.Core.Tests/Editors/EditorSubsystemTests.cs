using System;
using System.IO;
using System.Linq;
using System.Threading;
using ThwipKit.Core.Editors;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using ThwipKit.Core.Textures;
using Xunit;

namespace ThwipKit.Core.Tests;

public class EditorSubsystemTests
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "thwipkit-editors-" + Guid.NewGuid().ToString("N"));

    private GameBase CreateGame() => new ConfiguredGame(new GameDefinition
    {
        InternalId = "MSMR",
        ExecutableName = "Spider-Man Remastered.exe",
        ArchiveDirectory = "asset_archive",
        TocFileName = "TOC",
        TocFormat = TocFormat.ZlibDat1,
        CompressionFormats = [CompressionFormat.Lz4],
    });

    [Fact]
    public void EditorUndoSupport_TracksUndoAndRedo()
    {
        var undo = new EditorUndoSupport();
        undo.Initialize("file.txt", "alpha");

        Assert.False(undo.CanUndo("file.txt"));
        Assert.False(undo.CanRedo("file.txt"));

        undo.Record("file.txt", "beta");
        Assert.True(undo.CanUndo("file.txt"));
        Assert.Equal("alpha", undo.Undo("file.txt"));

        Assert.True(undo.CanRedo("file.txt"));
        Assert.Equal("beta", undo.Redo("file.txt"));

        Assert.False(undo.CanRedo("file.txt"));
    }

    [Fact]
    public void EditorUndoSupport_NewStateBranchesOverRedoHistory()
    {
        var undo = new EditorUndoSupport();
        undo.Initialize("file.txt", "a");
        undo.Record("file.txt", "b");
        undo.Record("file.txt", "c");
        undo.Undo("file.txt"); // back to "b"

        undo.Record("file.txt", "d"); // branches: redo of "c" discarded

        Assert.False(undo.CanRedo("file.txt"));
        Assert.Equal("b", undo.Undo("file.txt"));
    }

    [Fact]
    public void ExternalToolLauncher_NoConfiguredTool_ThrowsClearError()
    {
        string prefsPath = Path.Combine(_tempDir, "prefs.json");
        Directory.CreateDirectory(_tempDir);
        var launcher = new ExternalToolLauncher(new EditorPreferences(prefsPath));
        string file = Path.Combine(_tempDir, "image.dds");
        File.WriteAllText(file, "data");

        NotSupportedException ex = Assert.Throws<NotSupportedException>(() => launcher.Launch(file));
        Assert.Contains(".dds", ex.Message);
    }

    [Fact]
    public void ExternalToolLauncher_ConfiguredMissingTool_ThrowsFileNotFound()
    {
        string prefsPath = Path.Combine(_tempDir, "prefs.json");
        Directory.CreateDirectory(_tempDir);
        var prefs = new EditorPreferences(prefsPath);
        prefs.SetToolPath("dds", Path.Combine(_tempDir, "missing-tool.exe"));
        var launcher = new ExternalToolLauncher(prefs);
        string file = Path.Combine(_tempDir, "image.dds");
        File.WriteAllText(file, "data");

        Assert.Throws<FileNotFoundException>(() => launcher.Launch(file));
    }

    [Fact]
    public void EditorFileWatcher_RaisesEventOnExternalSave()
    {
        string file = Path.Combine(_tempDir, "watch.txt");
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(file, "v1");

        using var watcher = new EditorFileWatcher(file);
        var signal = new ManualResetEventSlim(false);
        watcher.FileChanged += (sender, args) => signal.Set();

        File.WriteAllText(file, "v2");

        Assert.True(signal.Wait(TimeSpan.FromSeconds(3)), "FileChanged event was not raised");
    }

    [Fact]
    public void TextureFormatConverter_ContainerRoundTripsLosslessly()
    {
        byte[] blockData = Enumerable.Range(0, 16).Select(i => (byte)(i * 7 + 3)).ToArray();
        byte[] original = BuildTextureBytes(4, 4, 1, TextureHeader.DxgiFormat.BC7_UNORM, blockData);

        byte[] dds = TextureFormatConverter.ConvertTextureToDds(original);
        byte[] back = TextureFormatConverter.ConvertDdsToTexture(dds);

        Assert.Equal(original, back);
        // DDS magic "DDS "
        Assert.Equal(0x20534444u, BitConverter.ToUInt32(dds, 0));
    }

    [Fact]
    public void TextureFormatConverter_Bc1CodecRoundTripsSolidColor()
    {
        byte[] rgba = new byte[4 * 4 * 4];
        for (int i = 0; i < 16; i++)
        {
            rgba[i * 4] = 255;
            rgba[i * 4 + 1] = 255;
            rgba[i * 4 + 2] = 255;
            rgba[i * 4 + 3] = 255;
        }

        byte[] texture = TextureFormatConverter.EncodePixelsToTexture(rgba, 4, 4, TextureHeader.DxgiFormat.BC1_UNORM);
        BCnEncoder.Shared.ColorRgba32[] pixels = TextureFormatConverter.DecodeTextureToPixels(texture);

        Assert.Equal(16, pixels.Length);
        foreach (BCnEncoder.Shared.ColorRgba32 p in pixels)
        {
            Assert.Equal(255, p.r);
            Assert.Equal(255, p.g);
            Assert.Equal(255, p.b);
            Assert.Equal(255, p.a);
        }
    }

    [Fact]
    public void EditorRegistry_RoutesUndoThroughInterface()
    {
        string prefsPath = Path.Combine(_tempDir, "prefs.json");
        Directory.CreateDirectory(_tempDir);
        var registry = new EditorRegistry(new EditorPreferences(prefsPath));
        var editor = new ConfigEditor();
        registry.Register(editor);

        Assert.True(registry.SupportsUndo(editor));
        editor.InitializeUndo("f", "one");
        editor.RecordChange("f", "two");

        Assert.Equal("one", registry.Undo(editor, "f"));
        Assert.Equal("two", registry.Redo(editor, "f"));
    }

    [Fact]
    public void EditorRegistry_LaunchExternalEditor_ThrowsWhenUnsupported()
    {
        string prefsPath = Path.Combine(_tempDir, "prefs.json");
        Directory.CreateDirectory(_tempDir);
        var registry = new EditorRegistry(new EditorPreferences(prefsPath));
        var editor = new ConfigEditor();
        registry.Register(editor);

        string file = Path.Combine(_tempDir, "x.config");
        File.WriteAllText(file, "{}");

        Assert.Throws<NotSupportedException>(() => registry.LaunchExternalEditor(editor, file));
    }

    [Fact]
    public void EditorPreferences_GetToolPathForFile_NormalizesExtension()
    {
        string prefsPath = Path.Combine(_tempDir, "prefs.json");
        Directory.CreateDirectory(_tempDir);
        var prefs = new EditorPreferences(prefsPath);
        prefs.SetToolPath("dds", @"C:\tools\texconv.exe");

        Assert.Equal(@"C:\tools\texconv.exe", prefs.GetToolPathForFile("screenshot.DDS"));
        Assert.Equal(@"C:\tools\texconv.exe", prefs.GetToolPathForFile("screenshot.dds"));
        Assert.Null(prefs.GetToolPathForFile("screenshot.png"));
    }

    [Fact]
    public void MaterialEditor_SuggestTextureReferences_NoHashTable_ReturnsEmpty()
    {
        var editor = new MaterialEditor(CreateGame());

        Assert.Empty(editor.SuggestTextureReferences(Path.Combine(_tempDir, "no-such-game"), "texture"));
    }

    private static byte[] BuildTextureBytes(uint width, uint height, uint mipCount, TextureHeader.DxgiFormat format, byte[] blockData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(0x00000020u);
        writer.Write(width);
        writer.Write(height);
        writer.Write(1u); // depth
        writer.Write(mipCount);
        writer.Write((uint)format);
        writer.Write(0u); // flags
        writer.Write(3u); // dimension
        for (int i = 0; i < 16; i++)
        {
            writer.Write((byte)0); // reserved
        }

        writer.Write(blockData);
        return ms.ToArray();
    }
}
