using System;
using System.Diagnostics;
using System.IO;

namespace ThwipKit.Core.Editors;

/// <summary>
/// Launches the editor-configured external tool for a given file. Tool paths are
/// resolved per file type via <see cref="EditorPreferences.GetToolPathForFile"/>.
/// </summary>
public sealed class ExternalToolLauncher
{
    private readonly EditorPreferences _preferences;

    public ExternalToolLauncher(EditorPreferences preferences)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    /// <summary>
    /// Launches the editor-configured external tool for <paramref name="filePath"/>,
    /// passing the file path as the (last) argument. Returns the process exit code,
    /// or throws a clear error when no tool is configured or the tool is missing.
    /// </summary>
    public int Launch(string filePath, string? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string? toolPath = _preferences.GetToolPathForFile(filePath);
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            string ext = Path.GetExtension(filePath);
            throw new NotSupportedException(
                $"No external tool is configured for '{ext}'. Configure one via EditorPreferences.");
        }

        if (!File.Exists(toolPath))
        {
            throw new FileNotFoundException($"Configured external tool not found: {toolPath}", toolPath);
        }

        string args = string.IsNullOrWhiteSpace(arguments)
            ? $"\"{filePath}\""
            : $"{arguments} \"{filePath}\"";

        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = args,
            UseShellExecute = false
        }) ?? throw new IOException($"Failed to launch external tool: {toolPath}");

        process.WaitForExit();
        return process.ExitCode;
    }
}
