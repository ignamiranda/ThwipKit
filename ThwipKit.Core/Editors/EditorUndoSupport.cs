using System;
using System.IO;

namespace ThwipKit.Core.Editors;

/// <summary>
/// File-snapshot undo/redo helper for editors that write content to disk.
/// Call <see cref="CaptureBeforeWrite"/> before overwriting a file to snapshot
/// its prior content, then <see cref="Undo"/> / <see cref="Redo"/> to move
/// through the per-file history.
/// </summary>
internal sealed class EditorUndoSupport
{
    private const int DefaultCapacity = 100;
    private readonly int _capacity;
    private readonly List<(string Path, string Content)> _undoStack = [];
    private readonly List<(string Path, string Content)> _redoStack = [];

    public EditorUndoSupport(int capacity = DefaultCapacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Snapshots the file's current content before it is overwritten.
    /// Files that do not yet exist are recorded as empty.
    /// </summary>
    public void CaptureBeforeWrite(string filePath)
    {
        string content = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
        _undoStack.Add((filePath, content));
        if (_undoStack.Count > _capacity)
        {
            _undoStack.RemoveAt(0);
        }

        _redoStack.Clear();
    }

    /// <summary>
    /// Restores the most recently snapshotted content to its file and returns its path.
    /// Throws when there is nothing to undo.
    /// </summary>
    public string Undo()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("Nothing to undo.");
        }

        (string path, string content) = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _redoStack.Add((path, File.Exists(path) ? File.ReadAllText(path) : string.Empty));
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Re-applies the most recent undo and returns the restored file's path.
    /// Throws when there is nothing to redo.
    /// </summary>
    public string Redo()
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("Nothing to redo.");
        }

        (string path, string content) = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _undoStack.Add((path, File.Exists(path) ? File.ReadAllText(path) : string.Empty));
        File.WriteAllText(path, content);
        return path;
    }
}
