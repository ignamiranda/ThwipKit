using System;
using System.Collections.Generic;

namespace ThwipKit.Core.Editors;

/// <summary>
/// Per-file undo/redo history of textual content snapshots.
/// Editors call <see cref="Initialize"/> on load, <see cref="Record"/> after each
/// successful save, and <see cref="Undo"/>/<see cref="Redo"/> to retrieve a
/// prior/future snapshot to write back to disk.
/// </summary>
public sealed class EditorUndoSupport
{
    private readonly Dictionary<string, List<string>> _history = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _index = new(StringComparer.OrdinalIgnoreCase);

    public bool CanUndo(string filePath)
        => _index.TryGetValue(filePath, out int i) && i > 0;

    public bool CanRedo(string filePath)
        => _index.TryGetValue(filePath, out int i)
           && _history.TryGetValue(filePath, out var h)
           && i < h.Count - 1;

    public void Initialize(string filePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _history[filePath] = [content];
        _index[filePath] = 0;
    }

    public void Record(string filePath, string newContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!_history.TryGetValue(filePath, out var history))
        {
            history = [];
            _history[filePath] = history;
            _index[filePath] = -1;
        }

        int idx = _index[filePath];
        if (idx < history.Count - 1)
        {
            history.RemoveRange(idx + 1, history.Count - idx - 1);
        }

        history.Add(newContent);
        _index[filePath] = history.Count - 1;
    }

    public string? Undo(string filePath)
    {
        if (!CanUndo(filePath))
        {
            return null;
        }

        int idx = --_index[filePath];
        return _history[filePath][idx];
    }

    public string? Redo(string filePath)
    {
        if (!CanRedo(filePath))
        {
            return null;
        }

        int idx = ++_index[filePath];
        return _history[filePath][idx];
    }
}
