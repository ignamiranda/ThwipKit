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

    [Fact]
    public void ConfigEditor_SearchAndReplace_FindsAndReplacesText()
    {
        string json = "{\"name\": \"test\", \"value\": \"old\"}";
        var editor = new ConfigEditor();

        var (result, count) = editor.SearchAndReplace(json, "old", "new");

        Assert.Equal(1, count);
        Assert.Contains("\"value\": \"new\"", result);
        Assert.DoesNotContain("\"value\": \"old\"", result);
    }

    [Fact]
    public void ConfigEditor_SearchAndReplace_CaseInsensitiveByDefault()
    {
        string json = "{\"name\": \"TEST\", \"value\": \"Old\"}";
        var editor = new ConfigEditor();

        var (result, count) = editor.SearchAndReplace(json, "old", "new");

        Assert.Equal(1, count);
        Assert.Contains("\"value\": \"new\"", result);
        Assert.DoesNotContain("\"value\": \"Old\"", result);
    }

    [Fact]
    public void ConfigEditor_SearchAndReplace_CaseSensitiveWhenSpecified()
    {
        string json = "{\"name\": \"TEST\", \"value\": \"Old\"}";
        var editor = new ConfigEditor();

        var (result, count) = editor.SearchAndReplace(json, "old", "new", caseSensitive: true);

        Assert.Equal(0, count);
        Assert.Equal(json, result);
    }

    [Fact]
    public void ConfigEditor_SearchAndReplace_MultipleOccurrences()
    {
        string json = "{\"test\": \"old\", \"other\": \"old\"}";
        var editor = new ConfigEditor();

        var (result, count) = editor.SearchAndReplace(json, "old", "new");

        Assert.Equal(2, count);
        Assert.Contains("\"test\": \"new\"", result);
        Assert.Contains("\"other\": \"new\"", result);
    }

    [Fact]
    public void ConfigEditor_SearchAndReplace_EmptySearchTerm()
    {
        string json = "{\"test\": \"value\"}";
        var editor = new ConfigEditor();

        var (result, count) = editor.SearchAndReplace(json, "", "new");

        Assert.Equal(0, count);
        Assert.Equal(json, result);
    }

    [Fact]
    public void ConfigEditor_Diff_IdenticalContent()
    {
        string json = "{\"test\": \"value\"}";
        var editor = new ConfigEditor();

        string diff = editor.Diff(json, json);

        Assert.Empty(diff);
    }

    [Fact]
    public void ConfigEditor_Diff_SimpleChange()
    {
        string original = "{\"test\": \"old\"}";
        string modified = "{\"test\": \"new\"}";
        var editor = new ConfigEditor();

        string diff = editor.Diff(original, modified);

        Assert.Contains("- {\"test\": \"old\"}", diff);
        Assert.Contains("+ {\"test\": \"new\"}", diff);
    }

    [Fact]
    public void ConfigEditor_Diff_MultipleChanges()
    {
        string original = "{\"a\": 1, \"b\": \"old\"}";
        string modified = "{\"a\": 2, \"b\": \"new\"}";
        var editor = new ConfigEditor();

        string diff = editor.Diff(original, modified);

        Assert.Contains("- {\"a\": 1, \"b\": \"old\"}", diff);
        Assert.Contains("+ {\"a\": 2, \"b\": \"new\"}", diff);
    }

    [Fact]
    public void ConfigEditor_Diff_AdditionAndRemoval()
    {
        string original = "{\"a\": 1}";
        string modified = "{\"a\": 1, \"b\": 2}";
        var editor = new ConfigEditor();

        string diff = editor.Diff(original, modified);

        Assert.Contains("- {\"a\": 1}", diff);
        Assert.Contains("+ {\"a\": 1, \"b\": 2}", diff);
    }
}
