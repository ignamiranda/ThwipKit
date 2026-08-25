using System;
using System.IO;
using System.Text.Json;
using ThwipKit.Core.Games;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Editors;

public sealed class TextureEditor : IAssetEditor, IUndoCapableEditor, IExternalEditorLauncher
{
    private readonly ArchiveManager _archiveManager;
    private readonly AssetValidator _validator;
    private readonly EditorUndoSupport _undo = new();
    private readonly ExternalToolLauncher _launcher = new(new EditorPreferences());

    public EditorCapabilities Capabilities { get; } = new()
    {
        EditorName = "Texture Editor",
        FileExtensions = [".texture", ".dds", ".png"],
        CanEdit = true,
        CanValidate = true,
        RequiresExternalTool = false
    };

    public TextureEditor(ArchiveManager archiveManager, GameBase game)
    {
        _archiveManager = archiveManager ?? throw new ArgumentNullException(nameof(archiveManager));
        _validator = new AssetValidator(game ?? throw new ArgumentNullException(nameof(game)));
    }

    public bool CanHandle(string filePath)
        => Path.GetExtension(filePath) is ".texture" or ".png" or ".dds";

    public ValidationResult Validate(string filePath)
        => _validator.ValidateAsset(filePath);

    public void ExtractToPng(string gamePath, string textureName, string outputPngPath)
        => _archiveManager.ExtractTextureToPng(gamePath, textureName, outputPngPath);

    public void RebuildFromPng(string gamePath, string textureName, string inputPngPath, bool createBackup = true)
    {
        ValidationResult validation = _validator.ValidateAsset(inputPngPath);
        if (!validation.IsValid)
        {
            throw new InvalidDataException($"Cannot rebuild from invalid PNG: {string.Join("; ", validation.Errors)}");
        }

        _archiveManager.RebuildTextureFromPng(gamePath, textureName, inputPngPath, createBackup);
    }

    public bool RestoreFromBackup(string gamePath, string textureName)
        => _archiveManager.RestoreTextureFromBackup(gamePath, textureName);

    public bool SupportsUndo => true;

    public void InitializeUndo(string filePath, string content) => _undo.Initialize(filePath, content);

    public void RecordChange(string filePath, string content) => _undo.Record(filePath, content);

    public bool CanUndo(string filePath) => _undo.CanUndo(filePath);

    public bool CanRedo(string filePath) => _undo.CanRedo(filePath);

    public string? Undo(string filePath) => _undo.Undo(filePath);

    public string? Redo(string filePath) => _undo.Redo(filePath);

    public int LaunchExternalEditor(string filePath) => _launcher.Launch(filePath);
}

public sealed class ConfigEditor : IAssetEditor, IUndoCapableEditor, IExternalEditorLauncher
{
    private readonly EditorUndoSupport _undo = new();
    private readonly ExternalToolLauncher _launcher = new(new EditorPreferences());

    public EditorCapabilities Capabilities { get; } = new()
    {
        EditorName = "Config Editor",
        FileExtensions = [".config", ".json"],
        CanEdit = true,
        CanValidate = true,
        RequiresExternalTool = false
    };

    public bool CanHandle(string filePath)
        => Path.GetExtension(filePath) is ".config" or ".json";

    public ValidationResult Validate(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return ValidationResult.Failure($"File not found: {filePath}");
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(filePath));
            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON: {ex.Message}");
        }
    }

    public string Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Normalize to indented JSON so configs round-trip readably
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(filePath));
        return JsonSerializer.Serialize(doc.RootElement.Clone(), new JsonSerializerOptions { WriteIndented = true });
    }

    public void Save(string filePath, string jsonContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonContent);

        // Validate before writing so we never persist malformed JSON
        using JsonDocument doc = JsonDocument.Parse(jsonContent);
        string normalized = JsonSerializer.Serialize(doc.RootElement.Clone(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, normalized);
    }

    public (string ModifiedContent, int ReplacementsCount) SearchAndReplace(string content, string searchTerm, string replaceTerm, bool caseSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(content))
            return (content, 0);

        if (string.IsNullOrWhiteSpace(searchTerm))
            return (content, 0);

        StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int index = 0;
        int count = 0;
        string result = content;

        while ((index = result.IndexOf(searchTerm, index, comparison)) != -1)
        {
            result = result.Remove(index, searchTerm.Length).Insert(index, replaceTerm);
            index += replaceTerm.Length;
            count++;
        }

        return (result, count);
    }

    public string Diff(string originalContent, string modifiedContent)
    {
        if (originalContent == modifiedContent)
            return string.Empty;

        var originalLines = originalContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var modifiedLines = modifiedContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var diffLines = new List<string>();
        int maxLines = Math.Max(originalLines.Length, modifiedLines.Length);

        for (int i = 0; i < maxLines; i++)
        {
            string originalLine = i < originalLines.Length ? originalLines[i] : string.Empty;
            string modifiedLine = i < modifiedLines.Length ? modifiedLines[i] : string.Empty;

            if (originalLine == modifiedLine)
            {
                diffLines.Add("  " + originalLine);
            }
            else
            {
                if (!string.IsNullOrEmpty(originalLine))
                    diffLines.Add("- " + originalLine);
                if (!string.IsNullOrEmpty(modifiedLine))
                    diffLines.Add("+ " + modifiedLine);
            }
        }

        return string.Join(Environment.NewLine, diffLines);
    }

    public bool SupportsUndo => true;

    public void InitializeUndo(string filePath, string content) => _undo.Initialize(filePath, content);

    public void RecordChange(string filePath, string content) => _undo.Record(filePath, content);

    public bool CanUndo(string filePath) => _undo.CanUndo(filePath);

    public bool CanRedo(string filePath) => _undo.CanRedo(filePath);

    public string? Undo(string filePath) => _undo.Undo(filePath);

    public string? Redo(string filePath) => _undo.Redo(filePath);

    public int LaunchExternalEditor(string filePath) => _launcher.Launch(filePath);
}
