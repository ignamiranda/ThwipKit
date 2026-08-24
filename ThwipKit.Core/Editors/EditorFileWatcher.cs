using System;
using System.IO;

namespace ThwipKit.Core.Editors;

public sealed class ExternalFileChangedEventArgs : EventArgs
{
    public string FilePath { get; }
    public DateTime ChangedAt { get; }

    public ExternalFileChangedEventArgs(string filePath, DateTime changedAt)
    {
        FilePath = filePath;
        ChangedAt = changedAt;
    }
}

/// <summary>
/// Watches a single file on disk and raises <see cref="FileChanged"/> when an
/// external editor modifies it, so the in-app editor can prompt to reload.
/// </summary>
public sealed class EditorFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly string _filePath;
    private bool _disposed;

    public event EventHandler<ExternalFileChangedEventArgs>? FileChanged;

    public EditorFileWatcher(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Cannot watch a file that does not exist", filePath);
        }

        _filePath = filePath;
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath)!, Path.GetFileName(filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
        => FileChanged?.Invoke(this, new ExternalFileChangedEventArgs(_filePath, DateTime.UtcNow));

    private void OnRenamed(object? sender, RenamedEventArgs e)
        => FileChanged?.Invoke(this, new ExternalFileChangedEventArgs(_filePath, DateTime.UtcNow));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Changed -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Dispose();
    }
}
