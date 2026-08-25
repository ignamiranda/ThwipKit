using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ThwipKit.Core.Games;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Editors;

public sealed class MaterialEditor : IAssetEditor, IUndoCapableEditor, IExternalEditorLauncher
{
    private readonly AssetValidator _validator;
    private readonly GameBase _game;
    private readonly EditorUndoSupport _undo = new();
    private readonly ExternalToolLauncher _launcher = new(new EditorPreferences());

    public EditorCapabilities Capabilities { get; } = new()
    {
        EditorName = "Material Editor",
        FileExtensions = [".material"],
        CanEdit = true,
        CanValidate = true,
        RequiresExternalTool = false
    };

    public MaterialEditor(GameBase game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _validator = new AssetValidator(_game);
    }

    public bool CanHandle(string filePath)
        => Path.GetExtension(filePath).Equals(".material", StringComparison.OrdinalIgnoreCase);

    public ValidationResult Validate(string filePath) => _validator.ValidateAsset(filePath);

    /// <summary>
    /// Loads the texture references declared in a material manifest.
    /// Materials are stored as JSON manifests describing ShaderTextures/overrides;
    /// raw binary .material payloads are not parsed in-process.
    /// </summary>
    public MaterialManifest Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Material file not found", filePath);
        }

        string content = File.ReadAllText(filePath);
        try
        {
            return JsonSerializer.Deserialize<MaterialManifest>(content)
                ?? throw new InvalidDataException($"Material manifest '{filePath}' is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Material manifest '{filePath}' is not valid JSON: {ex.Message}", ex);
        }
    }

    public void Save(string filePath, MaterialManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        manifest.Validate();
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(filePath, JsonSerializer.Serialize(manifest, options));
    }

    /// <summary>
    /// Suggests existing texture references known to the game (resolved from the
    /// community hash table) that contain <paramref name="partial"/>. Used to
    /// autocomplete ShaderTexture slots in the editor UI.
    /// </summary>
    public IReadOnlyList<string> SuggestTextureReferences(string gamePath, string partial, int maxSuggestions = 50)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gamePath);

        IReadOnlyDictionary<string, string>? hashTable = LoadHashTable(gamePath);
        if (hashTable is null)
        {
            return [];
        }

        string needle = (partial ?? string.Empty).Trim().ToLowerInvariant();
        var matches = new List<string>();

        foreach (string name in hashTable.Values)
        {
            if (needle.Length == 0 || name.ToLowerInvariant().Contains(needle))
            {
                matches.Add(name);
                if (matches.Count >= maxSuggestions)
                {
                    break;
                }
            }
        }

        return matches;
    }

    public bool SupportsUndo => true;

    public void InitializeUndo(string filePath, string content) => _undo.Initialize(filePath, content);

    public void RecordChange(string filePath, string content) => _undo.Record(filePath, content);

    public bool CanUndo(string filePath) => _undo.CanUndo(filePath);

    public bool CanRedo(string filePath) => _undo.CanRedo(filePath);

    public string? Undo(string filePath) => _undo.Undo(filePath);

    public string? Redo(string filePath) => _undo.Redo(filePath);

    public int LaunchExternalEditor(string filePath) => _launcher.Launch(filePath);

    private IReadOnlyDictionary<string, string>? LoadHashTable(string gamePath)
    {
        if (_hashTableCache.TryGetValue(gamePath, out IReadOnlyDictionary<string, string>? cached))
        {
            return cached;
        }

        IReadOnlyDictionary<string, string>? loaded;
        try
        {
            loaded = _game.LoadHashTable(gamePath);
        }
        catch
        {
            loaded = null;
        }

        _hashTableCache[gamePath] = loaded;
        return loaded;
    }

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>?> _hashTableCache = [];
}

public sealed class MaterialManifest
{
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>SectionType distinguishes material files from templates.</summary>
    public string SectionType { get; set; } = "Material";

    public bool HasUniversalHeader { get; set; }

    public List<ShaderFloatEntry> ShaderFloats { get; set; } = [];

    public List<ShaderIntegerEntry> ShaderIntegers { get; set; } = [];

    public List<TextureReference> ShaderTextures { get; set; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MaterialName))
        {
            throw new InvalidDataException("Material name is required.");
        }

        foreach (TextureReference texture in ShaderTextures)
        {
            texture.Validate();
        }
    }
}

public sealed class ShaderFloatEntry
{
    public required string Name { get; set; }
    public float Value { get; set; }
}

public sealed class ShaderIntegerEntry
{
    public required string Name { get; set; }
    public int Value { get; set; }
}

public sealed class TextureReference
{
    public required string SlotName { get; set; }
    public string TexturePathOrHash { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SlotName))
        {
            throw new InvalidDataException("Texture reference slot name is required.");
        }
        if (string.IsNullOrWhiteSpace(TexturePathOrHash))
        {
            throw new InvalidDataException($"Texture reference '{SlotName}' has no texture path or hash.");
        }
    }
}

public sealed class ModelEditor : IAssetEditor
{
    private readonly EditorPreferences _preferences;

    public EditorCapabilities Capabilities { get; } = new()
    {
        EditorName = "Model Editor",
        FileExtensions = [".model", ".ascii"],
        CanEdit = false,
        CanValidate = true,
        RequiresExternalTool = true
    };

    public ModelEditor(EditorPreferences preferences)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public bool CanHandle(string filePath)
        => Path.GetExtension(filePath) is ".model" or ".ascii";

    public ValidationResult Validate(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return ValidationResult.Failure($"File not found: {filePath}");
        }

        string? converterPath = _preferences.GetToolPath("model");
        if (string.IsNullOrWhiteSpace(converterPath))
        {
            var result = new ValidationResult();
            result.Warnings.Add("No external model converter configured. Set it via EditorPreferences.");
            if (new FileInfo(filePath).Length == 0)
            {
                result.Errors.Add("File is empty");
            }
            return result;
        }

        if (!File.Exists(converterPath))
        {
            return ValidationResult.Failure($"Configured converter not found: {converterPath}");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Launches the configured external converter for this model file.
    /// Returns the process exit code, or throws when no converter is configured.
    /// </summary>
    public int Convert(string filePath)
    {
        string? converterPath = _preferences.GetToolPath("model")
            ?? throw new NotSupportedException(
                "No external model converter is configured. Model conversion requires an external tool; set its path via EditorPreferences.");

        if (!File.Exists(converterPath))
        {
            throw new FileNotFoundException("Configured converter not found", converterPath);
        }

        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = converterPath,
            Arguments = $"\"{filePath}\"",
            UseShellExecute = false
        }) ?? throw new IOException($"Failed to launch converter: {converterPath}");

        process.WaitForExit();
        return process.ExitCode;
    }
}
