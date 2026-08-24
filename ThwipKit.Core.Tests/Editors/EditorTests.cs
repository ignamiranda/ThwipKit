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
    public void ConfigEditor_SaveUndoRedo_RestoresPriorContent()
    {
        string path = Path.Combine(_tempDir, "undo.config");
        File.WriteAllText(path, "{\"a\":1}");

        var editor = new ConfigEditor();
        Assert.False(editor.CanUndo);
        Assert.True(editor.Capabilities.SupportsUndo);

        editor.Save(path, "{\"b\":2}");
        Assert.True(editor.CanUndo);
        Assert.Contains("\"b\"", File.ReadAllText(path));

        editor.Undo();
        Assert.Contains("\"a\"", File.ReadAllText(path));

        editor.Redo();
        Assert.Contains("\"b\"", File.ReadAllText(path));
    }

    [Fact]
    public void MaterialEditor_SaveUndo_RestoresPriorManifest()
    {
        string path = Path.Combine(_tempDir, "undo.material");
        _materialEditor.Save(path, new MaterialManifest { MaterialName = "Original" });

        _materialEditor.Save(path, new MaterialManifest { MaterialName = "Modified" });
        Assert.True(_materialEditor.CanUndo);

        _materialEditor.Undo();
        Assert.Equal("Original", _materialEditor.Load(path).MaterialName);

        _materialEditor.Redo();
        Assert.Equal("Modified", _materialEditor.Load(path).MaterialName);
    }

    [Fact]
    public void TextureEditor_Undo_ThrowsNotSupported()
    {
        var archiveManager = new ArchiveManager(_game);
        var editor = new TextureEditor(archiveManager, _game);

        Assert.False(editor.Capabilities.SupportsUndo);
        Assert.False(editor.CanUndo);
        Assert.Throws<NotSupportedException>(() => editor.Undo());
        Assert.Throws<NotSupportedException>(() => editor.Redo());
    }

    [Fact]
    public void ConfigEditor_LaunchExternalEditor_WithoutTool_ThrowsNotSupported()
    {
        var editor = new ConfigEditor();
        Assert.Throws<NotSupportedException>(() => editor.LaunchExternalEditor(Path.Combine(_tempDir, "x.config")));
    }

    [Fact]
    public void ConfigEditor_LaunchExternalEditor_MissingTool_ThrowsFileNotFound()
    {
        string prefsPath = Path.Combine(_tempDir, "prefs-missing.json");
        var prefs = new EditorPreferences(prefsPath);
        prefs.SetToolPath(".config", Path.Combine(_tempDir, "does-not-exist.exe"));

        var editor = new ConfigEditor(prefs);
        Assert.Throws<FileNotFoundException>(() => editor.LaunchExternalEditor(Path.Combine(_tempDir, "x.config")));
    }

    [Fact]
    public void ModelEditor_LaunchExternalEditor_WithoutTool_ThrowsWithClearMessage()
    {
        var prefs = new EditorPreferences(Path.Combine(_tempDir, "prefs-model.json"));
        string modelPath = Path.Combine(_tempDir, "hero.model");
        File.WriteAllBytes(modelPath, [0x01]);

        var editor = new ModelEditor(prefs);
        NotSupportedException ex = Assert.Throws<NotSupportedException>(() => editor.LaunchExternalEditor(modelPath));
        Assert.Contains("external model converter", ex.Message);
    }

    [Fact]
    public void EditorPreferences_GetToolPathForFile_NormalizesExtensionKey()
    {
        string prefsPath = Path.Combine(_tempDir, "prefs-norm.json");
        var prefs = new EditorPreferences(prefsPath);
        prefs.SetToolPath("png", "C:\\tools\\aseprite.exe");

        Assert.Equal("C:\\tools\\aseprite.exe", prefs.GetToolPathForFile("textures/suit.png"));
        Assert.Equal("C:\\tools\\aseprite.exe", prefs.GetToolPathForFile("SUIT.PNG"));
        Assert.Null(prefs.GetToolPathForFile("model.model"));
    }

    [Fact]
    public void EditorPreferences_Load_NormalizesLegacyKeysWithoutDot()
    {
        string prefsPath = Path.Combine(_tempDir, "prefs-legacy.json");
        File.WriteAllText(prefsPath, "{ \"material\": \"C:/tools/mat.exe\", \"PNG\": \"C:/tools/png.exe\" }");

        var prefs = new EditorPreferences(prefsPath);

        Assert.Equal("C:/tools/mat.exe", prefs.GetToolPathForFile("suit.material"));
        Assert.Equal("C:/tools/png.exe", prefs.GetToolPathForFile("suit.png"));
    }

    [Fact]
    public void EditorRegistry_UndoRedo_DispatchesToMatchingEditor()
    {
        var editor = new ConfigEditor();
        var registry = new EditorRegistry();
        registry.Register(editor);

        string path = Path.Combine(_tempDir, "dispatch.config");
        File.WriteAllText(path, "{\"v\":1}");
        editor.Save(path, "{\"v\":2}");

        Assert.True(registry.CanUndo(path));
        Assert.False(registry.CanUndo("file.unknown"));

        registry.Undo(path);
        Assert.Contains("\"v\":1", File.ReadAllText(path));

        registry.Redo(path);
        Assert.Contains("\"v\": 2", File.ReadAllText(path));
    }

    [Fact]
    public void EditorPreferences_PersistsToolPathsAcrossInstances()
    {
        string prefsPath = Path.Combine(_tempDir, "prefs-roundtrip.json");
        var first = new EditorPreferences(prefsPath);
        first.SetToolPath(".png", "C:/tools/aseprite.exe");

        var second = new EditorPreferences(prefsPath);

        Assert.Equal("C:/tools/aseprite.exe", second.GetToolPathForFile("textures/suit.png"));
    }

    [Fact]
    public void EditorRegistry_LaunchExternalEditor_WithoutEditor_Throws()
    {
        var registry = new EditorRegistry();
        Assert.Throws<NotSupportedException>(() => registry.LaunchExternalEditor("file.unknown"));
    }
}
