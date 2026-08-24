using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThwipKit.Core;

namespace ThwipKit.Wpf.Services;

public sealed class RecentProjectsStore
{
    private const int MaxEntries = 10;
    private readonly string _path;
    private readonly List<string> _entries = [];

    public RecentProjectsStore()
    {
        _path = Path.Combine(AppSettings.GetSettingsDirectory(), "recentprojects.txt");
        Load();
    }

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        _entries.Remove(projectPath);
        _entries.Insert(0, projectPath);
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        Save();
    }

    public void Remove(string projectPath)
    {
        if (_entries.Remove(projectPath))
        {
            Save();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        foreach (string line in File.ReadAllLines(_path))
        {
            if (!string.IsNullOrWhiteSpace(line) && File.Exists(line))
            {
                _entries.Add(line);
            }
        }
    }

    private void Save()
    {
        try
        {
            string directory = Path.GetDirectoryName(_path) ?? ".";
            Directory.CreateDirectory(directory);
            File.WriteAllLines(_path, _entries);
        }
        catch (IOException)
        {
        }
    }
}
