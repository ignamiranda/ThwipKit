using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Staging;

public sealed class TempFileManager : IDisposable
{
    private readonly string _tempRoot;
    private readonly List<string> _trackedFiles = [];
    private bool _disposed;

    public TempFileManager(string? tempRoot = null)
    {
        _tempRoot = tempRoot ?? Path.Combine(Path.GetTempPath(), "SpiderManModTool");
        if (!Directory.Exists(_tempRoot))
        {
            Directory.CreateDirectory(_tempRoot);
        }
    }

    public string CreateTempFile(string? prefix = null, string? extension = null)
    {
        string fileName = prefix ?? Guid.NewGuid().ToString("N");
        if (!string.IsNullOrEmpty(extension) && !extension.StartsWith("."))
        {
            extension = $".{extension}";
        }

        string tempPath = Path.Combine(_tempRoot, $"{fileName}{extension}");
        File.WriteAllBytes(tempPath, []);
        _trackedFiles.Add(tempPath);
        return tempPath;
    }

    public string CreateTempFileWithContent(byte[] content, string? prefix = null, string? extension = null)
    {
        string tempPath = CreateTempFile(prefix, extension);
        File.WriteAllBytes(tempPath, content);
        return tempPath;
    }

    public string CreateTempDirectory(string? prefix = null)
    {
        string dirName = prefix ?? Guid.NewGuid().ToString("N");
        string tempDir = Path.Combine(_tempRoot, dirName);
        Directory.CreateDirectory(tempDir);
        _trackedFiles.Add(tempDir);
        return tempDir;
    }

    public void TrackFile(string filePath)
    {
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            _trackedFiles.Add(filePath);
        }
    }

    public void SecureDelete(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return;
        }

        try
        {
            // Overwrite with zeros before deletion
            long length = new FileInfo(filePath).Length;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write))
            {
                fs.SetLength(0);
                fs.Write(new byte[length], 0, (int)Math.Min(length, int.MaxValue));
            }
            File.Delete(filePath);
            _trackedFiles.Remove(filePath);
        }
        catch
        {
            // Best effort
        }
    }

    public void Cleanup()
    {
        foreach (string filePath in _trackedFiles.ToArray())
        {
            try
            {
                if (File.Exists(filePath))
                {
                    SecureDelete(filePath);
                }
                else if (Directory.Exists(filePath))
                {
                    Directory.Delete(filePath, recursive: true);
                    _trackedFiles.Remove(filePath);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
        _trackedFiles.Clear();
    }

    public void CleanupAllTempFiles()
    {
        if (Directory.Exists(_tempRoot))
        {
            foreach (string file in Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories))
            {
                try { SecureDelete(file); } catch { }
            }
            foreach (string dir in Directory.GetDirectories(_tempRoot, "*", SearchOption.AllDirectories))
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            _disposed = true;
        }
    }
}
