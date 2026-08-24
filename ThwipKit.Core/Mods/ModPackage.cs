using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace ThwipKit.Core.Mods;

public sealed class ModManifest
{
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public string TargetGame { get; set; } = "MSMR";
    public List<string> Dependencies { get; set; } = [];
    public List<ModFileEntry> Files { get; set; } = [];

    private static readonly JsonSerializerOptions s_options = new() { WriteIndented = true };

    public static ModManifest Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Mod manifest not found", filePath);
        }

        ModManifest? manifest = JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(filePath));
        return manifest ?? throw new InvalidDataException($"Mod manifest '{filePath}' is empty or invalid.");
    }

    public void Save(string filePath)
    {
        Validate();
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, JsonSerializer.Serialize(this, s_options));
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidDataException("Mod name is required.");
        }
        if (Files.Count == 0)
        {
            throw new InvalidDataException($"Mod '{Name}' must declare at least one file.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ModFileEntry entry in Files)
        {
            if (string.IsNullOrWhiteSpace(entry.RelativePath))
            {
                throw new InvalidDataException($"Mod '{Name}' has a file entry with no relative path.");
            }
            if (!seen.Add(entry.RelativePath))
            {
                throw new InvalidDataException($"Mod '{Name}' declares duplicate file path: {entry.RelativePath}");
            }
        }
    }

    public bool SatisfiesDependencies(IReadOnlyCollection<string> availableModNames)
    {
        foreach (string dependency in Dependencies)
        {
            if (!availableModNames.Contains(dependency, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }
}

public sealed class ModFileEntry
{
    /// <summary>Path of the file inside the package, using forward slashes.</summary>
    public required string RelativePath { get; set; }

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Asset type hint used by the installer (texture/model/material/config).</summary>
    public string AssetType { get; set; } = "unknown";
}

public sealed class ModPackage
{
    public const string ManifestFileName = "mod.json";
    public const string PackageExtension = ".spidermod";

    public ModManifest Manifest { get; }

    public string PackagePath { get; }

    private ModPackage(ModManifest manifest, string packagePath)
    {
        Manifest = manifest;
        PackagePath = packagePath;
    }

    public static ModPackage CreateFromDirectory(string sourceDirectory, string outputPackagePath)
    {
        ArgumentNullException.ThrowIfNull(outputPackagePath);

        string manifestPath = Path.Combine(sourceDirectory, ManifestFileName);
        ModManifest manifest = ModManifest.Load(manifestPath);

        // Sync file entries with what is actually on disk
        manifest.Files.Clear();
        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            if (file.Equals(manifestPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            manifest.Files.Add(new ModFileEntry
            {
                RelativePath = relativePath,
                SizeBytes = new FileInfo(file).Length
            });
        }
        manifest.Validate();
        manifest.Save(manifestPath);

        string? outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPackagePath));
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        using FileStream stream = new(outputPackagePath, FileMode.Create);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);
        {
            foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string entryName = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
                archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
            }
        }

        return new ModPackage(manifest, outputPackagePath);
    }

    public static ModPackage Open(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Mod package not found", packagePath);
        }

        using FileStream stream = new(packagePath, FileMode.Open, FileAccess.Read);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);
        ZipArchiveEntry? manifestEntry = archive.GetEntry(ManifestFileName)
            ?? throw new InvalidDataException($"Package '{packagePath}' does not contain {ManifestFileName}.");

        using StreamReader reader = new(manifestEntry.Open());
        ModManifest? manifest = JsonSerializer.Deserialize<ModManifest>(reader.ReadToEnd())
            ?? throw new InvalidDataException($"Manifest in '{packagePath}' is invalid.");

        return new ModPackage(manifest, packagePath);
    }

    public void ExtractTo(string destinationDirectory)
    {
        ArgumentNullException.ThrowIfNull(destinationDirectory);

        Directory.CreateDirectory(destinationDirectory);

        using FileStream stream = new(PackagePath, FileMode.Open, FileAccess.Read);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            // Zip-slip protection: resolve and verify the target stays under the destination
            string normalizedEntryPath = entry.FullName.Replace('\\', '/');
            if (normalizedEntryPath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(normalizedEntryPath))
            {
                throw new InvalidDataException($"Package contains unsafe entry path: {entry.FullName}");
            }

            string targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, normalizedEntryPath));
            if (!targetPath.StartsWith(Path.GetFullPath(destinationDirectory), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Package entry escapes destination directory: {entry.FullName}");
            }

            string? entryDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(entryDir))
            {
                Directory.CreateDirectory(entryDir);
            }

            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }
}
