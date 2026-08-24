using System;
using System.IO;
using ThwipKit.Core.Editors;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using Xunit;

namespace ThwipKit.Core.Tests;

public class EditorRegistryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly GameBase _game;
    private readonly MaterialEditor _materialEditor;

    public EditorRegistryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _game = new ConfiguredGame(new GameDefinition
        {
            InternalId = "MSMR",
            ExecutableName = "Spider-Man Remastered.exe",
            ArchiveDirectory = "asset_archive",
            TocFileName = "TOC",
            TocFormat = TocFormat.ZlibDat1,
            CompressionFormats = [CompressionFormat.Lz4],
        });
        _materialEditor = new MaterialEditor(_game);
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
    public void ConfigEditor_LoadSave_RoundTripsJson()
    {
        string path = Path.Combine(_tempDir, "test.config");
        File.WriteAllText(path, "{\"b\":2,\"a\":1}");

        var editor = new ConfigEditor();
        string normalized = editor.Load(path);

        Assert.Contains("\"a\": 1", normalized);
        Assert.Contains("\"b\": 2", normalized);
    }

    [Fact]
    public void ConfigEditor_Validate_RejectsMalformedJson()
    {
        string path = Path.Combine(_tempDir, "bad.config");
        File.WriteAllText(path, "{not valid json");

        var result = new ConfigEditor().Validate(path);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void ConfigEditor_Validate_AcceptsWellFormedJson()
    {
        string path = Path.Combine(_tempDir, "good.json");
        File.WriteAllText(path, "{}");

        Assert.True(new ConfigEditor().Validate(path).IsValid);
    }

    [Fact]
    public void ConfigEditor_Save_RejectsMalformedContent()
    {
        var editor = new ConfigEditor();
        Assert.ThrowsAny<Exception>(() => editor.Save(Path.Combine(_tempDir, "out.config"), "{broken"));
    }

    [Fact]
    public void MaterialEditor_SaveLoad_RoundTripsManifest()
    {
        string path = Path.Combine(_tempDir, "suit.material");

        var manifest = new MaterialManifest
        {
            MaterialName = "SuitA",
            ShaderFloats = [new ShaderFloatEntry { Name = "Roughness", Value = 0.5f }],
            ShaderTextures = [new TextureReference { SlotName = "BaseColor", TexturePathOrHash = "textures/suit_a_base.texture" }]
        };

        _materialEditor.Save(path, manifest);
        MaterialManifest loaded = _materialEditor.Load(path);

        Assert.Equal("SuitA", loaded.MaterialName);
        Assert.Single(loaded.ShaderTextures);
        Assert.Equal("BaseColor", loaded.ShaderTextures[0].SlotName);
        Assert.Equal(0.5f, loaded.ShaderFloats[0].Value);
    }

    [Fact]
    public void MaterialEditor_ManifestValidation_RequiresTexturePath()
    {
        var manifest = new MaterialManifest
        {
            MaterialName = "Bad",
            ShaderTextures = [new TextureReference { SlotName = "BaseColor", TexturePathOrHash = "" }]
        };

        Assert.Throws<InvalidDataException>(() => manifest.Validate());
    }

    [Fact]
    public void ModelEditor_ConvertWithoutConfiguredTool_ThrowsWithClearMessage()
    {
        var prefs = new EditorPreferences(Path.Combine(_tempDir, "prefs.json"));
        string modelPath = Path.Combine(_tempDir, "hero.model");
        File.WriteAllBytes(modelPath, [0x01]);

        var editor = new ModelEditor(prefs);

        NotSupportedException ex = Assert.Throws<NotSupportedException>(() => editor.Convert(modelPath));
        Assert.Contains("external model converter", ex.Message);
    }

    [Fact]
    public void EditorRegistry_FindsEditorByExtension()
    {
        var registry = new EditorRegistry();
        registry.Register(new ConfigEditor());
        registry.Register(_materialEditor);

        Assert.IsType<MaterialEditor>(registry.FindEditor("x/suit.material"));
        Assert.IsType<ConfigEditor>(registry.FindEditor("settings.config"));
        Assert.Null(registry.FindEditor("unknown.xyz"));
    }

    [Fact]
    public void EditorRegistry_ReportsSupportedExtensions()
    {
        var registry = new EditorRegistry();
        registry.Register(new ConfigEditor());
        registry.Register(_materialEditor);

        var extensions = registry.GetSupportedExtensions().ToList();

        Assert.Contains(".config", extensions);
        Assert.Contains(".json", extensions);
        Assert.Contains(".material", extensions);
    }

    [Fact]
    public void EditorRegistry_ValidateWithoutEditor_ReturnsError()
    {
        var registry = new EditorRegistry();
        ValidationResult result = registry.ValidateWithEditor("file.unknown");

        Assert.False(result.IsValid);
        Assert.Contains("No editor registered", result.Errors[0]);
    }
}
