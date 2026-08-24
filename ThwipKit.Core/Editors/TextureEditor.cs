using System;
using System.IO;
using System.Text.Json;
using ThwipKit.Core.Games;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Editors;

public sealed class TextureEditor : IAssetEditor
{
    private readonly ArchiveManager _archiveManager;
    private readonly AssetValidator _validator;

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
}

public sealed class ConfigEditor : IAssetEditor
{
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
}
