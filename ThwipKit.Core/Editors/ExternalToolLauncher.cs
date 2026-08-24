using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace ThwipKit.Core.Editors;

/// <summary>
/// Shared helper for editors that launch an external process for a file type.
/// </summary>
internal static class ExternalToolLauncher
{
    public static int Launch(string toolPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            throw new NotSupportedException("No external tool is configured for this file type.");
        }

        if (!File.Exists(toolPath))
        {
            throw new FileNotFoundException($"External tool not found: {toolPath}", toolPath);
        }

        try
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false
            }) ?? throw new IOException($"Failed to launch external tool: {toolPath}");

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception ex)
        {
            throw new IOException($"Failed to launch external tool: {toolPath}", ex);
        }
    }
}
