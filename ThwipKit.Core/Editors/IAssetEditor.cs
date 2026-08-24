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
    public bool SupportsUndo { get; init; }

    public bool HandlesExtension(string extension)
        => FileExtensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));
}

public interface IAssetEditor
{
    EditorCapabilities Capabilities { get; }

    bool CanHandle(string filePath);

    ValidationResult Validate(string filePath);

    /// <summary>
    /// Whether this editor currently has an undoable action available.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Reverts the most recent action, if any. Throws if <see cref="CanUndo"/> is false.
    /// </summary>
    void Undo();

    /// <summary>
    /// Redoes the most recent undone action, if any. Throws if no redo is available.
    /// </summary>
    void Redo();

    /// <summary>
    /// Launches the configured external tool for this file type.
    /// Throws <see cref="NotSupportedException"/> when the editor has no external tool configured.
    /// </summary>
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
