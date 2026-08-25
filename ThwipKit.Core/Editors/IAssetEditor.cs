using System;
using System.Collections.Generic;
using System.Linq;

namespace ThwipKit.Core.Editors;

public sealed class EditorCapabilities
{
    public required IReadOnlyList<string> FileExtensions { get; init; }
    public string EditorName { get; init; } = string.Empty;
    public bool CanEdit { get; init; }
    public bool CanValidate { get; init; }
    public bool RequiresExternalTool { get; init; }

    public bool HandlesExtension(string extension)
        => FileExtensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));
}

public interface IAssetEditor
{
    EditorCapabilities Capabilities { get; }

    bool CanHandle(string filePath);

    ValidationResult Validate(string filePath);
}

/// <summary>
/// Implemented by editors that keep an in-process undo/redo history for text assets.
/// Routed through <see cref="EditorRegistry"/> so callers don't need to know the
/// concrete editor type.
/// </summary>
public interface IUndoCapableEditor
{
    bool SupportsUndo { get; }

    void InitializeUndo(string filePath, string content);

    void RecordChange(string filePath, string content);

    bool CanUndo(string filePath);

    bool CanRedo(string filePath);

    string? Undo(string filePath);

    string? Redo(string filePath);
}

/// <summary>
/// Implemented by editors that can launch a configured external tool for a file.
/// Routed through <see cref="EditorRegistry.LaunchExternalEditor"/>.
/// </summary>
public interface IExternalEditorLauncher
{
    int LaunchExternalEditor(string filePath);
}

public sealed class ValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; } = [];

    public List<string> Warnings { get; } = [];

    public static ValidationResult Success() => new();

    public static ValidationResult Failure(string error)
    {
        var result = new ValidationResult();
        result.Errors.Add(error);
        return result;
    }
}
