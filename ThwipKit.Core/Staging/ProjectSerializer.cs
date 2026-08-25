using System;
using System.IO;
using System.Text.Json;

namespace ThwipKit.Core.Staging;

public static class ProjectSerializer
{
    public const string CurrentVersion = "1.0.0";

    public static void Save(string path, ModProject project)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(project);

        project.Metadata.ModifiedUtc = DateTime.UtcNow;

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new { version = CurrentVersion, project };
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static ModProject Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Project file not found", path);
        }

        string json = File.ReadAllText(path);
        return MigrateIfNeeded(json);
    }

    public static ModProject MigrateIfNeeded(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string version = root.TryGetProperty("version", out JsonElement versionElement)
            ? versionElement.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrEmpty(version))
        {
            throw new InvalidOperationException("Project file is missing a version stamp; cannot load.");
        }

        if (version != CurrentVersion)
        {
            throw new NotSupportedException($"Unsupported project version '{version}'. Only '{CurrentVersion}' is supported.");
        }

        if (!root.TryGetProperty("project", out JsonElement projectElement))
        {
            throw new InvalidDataException("Project file does not contain a 'project' element.");
        }

        ModProject? project = JsonSerializer.Deserialize<ModProject>(projectElement.GetRawText());
        if (project == null)
        {
            throw new InvalidDataException("Failed to deserialize project.");
        }

        if (project.SchemaVersion == 0)
        {
            project.SchemaVersion = 1;
        }

        return project;
    }
}