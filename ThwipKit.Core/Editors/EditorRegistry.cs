using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ThwipKit.Core.Editors;

public sealed class EditorPreferences
{
    private readonly string _settingsFilePath;
    private Dictionary<string, string> _toolPaths = new(StringComparer.OrdinalIgnoreCase);

    public EditorPreferences(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ThwipKit",
            "editor-preferences.json");
        Load();
    }

    public string SettingsFilePath => _settingsFilePath;

    public string? GetToolPath(string editorKey)
        => _toolPaths.TryGetValue(editorKey, out string? path) ? path : null;

    /// <summary>
    /// Resolves the tool path for a file's extension, normalizing the key
    /// (with or without a leading dot, case-insensitive).
    /// </summary>
    public string? GetToolPathForFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string extension = Path.GetExtension(filePath);
        return GetToolPath(extension);
    }

    /// <summary>
    /// Returns all file extensions that have a configured tool path.
    /// </summary>
    public IEnumerable<string> GetSupportedFileTypes()
        => _toolPaths.Keys.OrderBy(k => k);

    public void SetToolPath(string editorKey, string toolPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editorKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolPath);
        _toolPaths[NormalizeExtension(editorKey)] = toolPath;
        Save();
    }

    public void RemoveToolPath(string editorKey)
    {
        if (_toolPaths.Remove(NormalizeExtension(editorKey)))
        {
            Save();
        }
    }

    private static string NormalizeExtension(string key)
    {
        string k = key.Trim();
        if (k.Length == 0)
        {
            return k;
        }

        return k.StartsWith('.') ? k.ToLowerInvariant() : "." + k.ToLowerInvariant();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_settingsFilePath));
                if (loaded != null)
                {
                    _toolPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, string> kvp in loaded)
                    {
                        _toolPaths[NormalizeExtension(kvp.Key)] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable preferences fall back to defaults
            _toolPaths.Clear();
        }
    }

    private void Save()
    {
        string? directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(_toolPaths, new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>
/// Routes files to registered editors by extension and reports supported types.
/// </summary>
public sealed class EditorRegistry
{
    private readonly List<IAssetEditor> _editors = [];

    public IReadOnlyList<IAssetEditor> Editors => _editors.AsReadOnly();

    public void Register(IAssetEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _editors.Add(editor);
    }

    public IAssetEditor? FindEditor(string filePath)
        => _editors.FirstOrDefault(e => e.CanHandle(filePath));

    public IReadOnlyList<IAssetEditor> FindEditorsForExtension(string extension)
        => _editors.Where(e => e.Capabilities.HandlesExtension(extension)).ToList();

    public IEnumerable<string> GetSupportedExtensions()
        => _editors.SelectMany(e => e.Capabilities.FileExtensions).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(e => e);

    public ValidationResult ValidateWithEditor(string filePath)
    {
        IAssetEditor? editor = FindEditor(filePath);
        if (editor == null)
        {
            return ValidationResult.Failure($"No editor registered for '{Path.GetExtension(filePath)}'");
        }

        return editor.Validate(filePath);
    }

    public bool CanUndo(string filePath)
        => FindEditor(filePath)?.CanUndo ?? false;

    public void Undo(string filePath)
    {
        IAssetEditor? editor = FindEditor(filePath)
            ?? throw new NotSupportedException($"No editor registered for '{Path.GetExtension(filePath)}'");

        editor.Undo();
    }

    public void Redo(string filePath)
    {
        IAssetEditor? editor = FindEditor(filePath)
            ?? throw new NotSupportedException($"No editor registered for '{Path.GetExtension(filePath)}'");

        editor.Redo();
    }

    public int LaunchExternalEditor(string filePath)
    {
        IAssetEditor? editor = FindEditor(filePath)
            ?? throw new NotSupportedException($"No editor registered for '{Path.GetExtension(filePath)}'");

        return editor.LaunchExternalEditor(filePath);
    }
}
