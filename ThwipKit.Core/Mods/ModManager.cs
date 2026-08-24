using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ThwipKit.Core.Mods;

public sealed class InstalledMod
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime InstalledUtc { get; set; } = DateTime.UtcNow;
    public List<string> Dependencies { get; set; } = [];
}

public sealed class ModManager
{
    private const string RegistryFileName = "installed-mods.json";

    private readonly string _modsDirectory;

    public ModManager(string modsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modsDirectory);
        _modsDirectory = modsDirectory;
        Directory.CreateDirectory(_modsDirectory);
    }

    public string ModsDirectory => _modsDirectory;

    private string RegistryFilePath => Path.Combine(_modsDirectory, RegistryFileName);

    public IReadOnlyList<InstalledMod> GetInstalledMods()
        => LoadRegistry();

    public bool IsInstalled(string modName)
        => LoadRegistry().Any(m => m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Installs a .spidermod package: extracts files into mods/&lt;name&gt;/ and registers it.
    /// </summary>
    public InstalledMod Install(string packagePath)
    {
        ModPackage package = ModPackage.Open(packagePath);
        ModManifest manifest = package.Manifest;
        manifest.Validate();

        if (IsInstalled(manifest.Name))
        {
            throw new InvalidOperationException($"Mod '{manifest.Name}' is already installed. Uninstall it first.");
        }

        // Verify dependency availability before extracting anything
        var installedNames = LoadRegistry().Select(m => m.Name).ToList();
        if (!manifest.SatisfiesDependencies(installedNames))
        {
            string missing = string.Join(", ", manifest.Dependencies.Except(installedNames, StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException($"Mod '{manifest.Name}' has unmet dependencies: {missing}");
        }

        string targetDir = Path.Combine(_modsDirectory, manifest.Name);
        package.ExtractTo(targetDir);

        var record = new InstalledMod
        {
            Name = manifest.Name,
            Version = manifest.Version,
            Author = manifest.Author,
            Enabled = true,
            Dependencies = [.. manifest.Dependencies]
        };

        var registry = LoadRegistry();
        registry.Add(record);
        SaveRegistry(registry);

        return record;
    }

    public void Uninstall(string modName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modName);

        var registry = LoadRegistry();
        InstalledMod? record = registry.FirstOrDefault(m => m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Mod '{modName}' is not installed.");

        // Refuse to uninstall while others depend on it
        var dependents = registry.Where(m =>
            m.Enabled &&
            !m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase) &&
            m.Dependencies.Contains(modName, StringComparer.OrdinalIgnoreCase)).ToList();
        if (dependents.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot uninstall '{modName}' while enabled mods depend on it: {string.Join(", ", dependents.Select(d => d.Name))}");
        }

        string targetDir = Path.Combine(_modsDirectory, record.Name);
        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, recursive: true);
        }

        registry.Remove(record);
        SaveRegistry(registry);
    }

    public void SetEnabled(string modName, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modName);

        var registry = LoadRegistry();
        InstalledMod? record = registry.FirstOrDefault(m => m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Mod '{modName}' is not installed.");

        if (enabled && !record.Enabled && !SatisfiesDependencies(record, registry.Where(m => m.Enabled).Select(m => m.Name)))
        {
            throw new InvalidOperationException(
                $"Mod '{modName}' cannot be enabled: dependencies not satisfied by currently enabled mods.");
        }

        if (!enabled && record.Enabled)
        {
            var dependents = registry.Where(m =>
                m.Enabled &&
                !m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase) &&
                m.Dependencies.Contains(modName, StringComparer.OrdinalIgnoreCase)).ToList();
            if (dependents.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Mod '{modName}' cannot be disabled while these enabled mods depend on it: {string.Join(", ", dependents.Select(d => d.Name))}");
            }
        }

        record.Enabled = enabled;
        SaveRegistry(registry);
    }

    public string GetModContentPath(string modName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modName);
        if (!IsInstalled(modName))
        {
            throw new InvalidOperationException($"Mod '{modName}' is not installed.");
        }
        return Path.Combine(_modsDirectory, modName);
    }

    private static bool SatisfiesDependencies(InstalledMod mod, IEnumerable<string> availableModNames)
    {
        var names = availableModNames.ToList();
        return mod.Dependencies.All(d => names.Contains(d, StringComparer.OrdinalIgnoreCase));
    }

    private List<InstalledMod> LoadRegistry()
    {
        try
        {
            if (File.Exists(RegistryFilePath))
            {
                List<InstalledMod>? loaded = JsonSerializer.Deserialize<List<InstalledMod>>(File.ReadAllText(RegistryFilePath));
                return loaded ?? [];
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt registry falls back to empty rather than blocking all mod operations
        }
        return [];
    }

    private void SaveRegistry(List<InstalledMod> registry)
    {
        File.WriteAllText(RegistryFilePath, JsonSerializer.Serialize(registry, new JsonSerializerOptions { WriteIndented = true }));
    }
}
