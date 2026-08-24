using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;

namespace ThwipKit.Core;

public class GameVersionInfo
{
    public string VersionString { get; set; } = "Unknown";
    public Version? Version { get; set; }
    public string GamePath { get; set; } = string.Empty;
    public DateTime? ExecutableDate { get; set; }
    public string DistributionPlatform { get; set; } = "Unknown";
    public bool IsKnownVersion { get; set; }
    public bool IsProblematicVersion { get; set; }
    public string? WarningMessage { get; set; }

    public override string ToString() => $"{VersionString} ({DistributionPlatform})";
}

public class GameVersionDetector
{
    private static readonly Dictionary<string, string> ProblematicVersions = new()
    {
        ["1.0.0"] = "Initial release version — may have archive format differences. Update the game if possible.",
        ["1.1.0"] = "Early patch — some textures may use unsupported formats in this tool version."
    };

    private static readonly HashSet<string> KnownGoodVersions =
    [
        "1.2.0", "1.2.1", "1.2.2", "1.2.3", "1.3.0", "1.3.1", "1.4.0", "1.4.1", "1.5.0", "1.5.1"
    ];

    public GameVersionInfo DetectVersion(string gamePath)
    {
        GameBase game = GameFactory.CreateGameFromPath(gamePath);
        return DetectVersion(gamePath, game.Definition);
    }

    public GameVersionInfo DetectVersion(string gamePath, GameBase game) => DetectVersion(gamePath, game.Definition);

    public GameVersionInfo DetectVersion(string gamePath, GameDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var info = new GameVersionInfo { GamePath = gamePath };
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            info.WarningMessage = "Game directory not found or inaccessible.";
            return info;
        }

        info.DistributionPlatform = DetectPlatform(gamePath, profile.SteamAppId);
        bool detected = TryDetectFromExecutable(gamePath, profile, info);
        if (!detected && info.DistributionPlatform == "Steam")
        {
            detected = TryDetectFromSteamManifest(gamePath, profile.SteamAppId, info);
        }
        if (!detected)
        {
            detected = TryDetectFromVersionFiles(gamePath, profile.VersionFileNames, info);
        }
        if (detected && info.VersionString != "Unknown")
        {
            CheckVersionCompatibility(info);
        }
        if (!detected)
        {
            info.WarningMessage = "Could not determine game version. Some features may not work correctly.";
        }
        return info;
    }

    private static string DetectPlatform(string gamePath, int steamAppId)
    {
        string? parent = Directory.GetParent(gamePath)?.FullName;
        bool hasManifest = steamAppId > 0 &&
            (File.Exists(Path.Combine(gamePath, $"appmanifest_{steamAppId}.acf")) ||
             parent != null && File.Exists(Path.Combine(parent, $"appmanifest_{steamAppId}.acf")));
        if (hasManifest || Directory.GetFiles(gamePath, "*.acf").Length > 0 || gamePath.Contains("steamapps", StringComparison.OrdinalIgnoreCase))
        {
            return "Steam";
        }
        if (Directory.Exists(Path.Combine(gamePath, ".egstore")) || gamePath.Contains("Epic Games", StringComparison.OrdinalIgnoreCase))
        {
            return "Epic Games";
        }
        return "Standalone";
    }

    private static bool TryDetectFromExecutable(string gamePath, GameDefinition profile, GameVersionInfo info)
    {
        IEnumerable<string> executables = new[] { profile.ExecutableName }.Concat(profile.SupportedExecutables)
            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (string executable in executables)
        {
            string path = Path.Combine(gamePath, executable);
            if (!File.Exists(path))
            {
                continue;
            }
            info.ExecutableDate = File.GetLastWriteTime(path);
            try
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                string? value = !string.IsNullOrWhiteSpace(version.FileVersion) ? version.FileVersion : version.ProductVersion;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    SetVersion(info, value);
                    return true;
                }
            }
            catch
            {
            }
        }
        return false;
    }

    private static bool TryDetectFromSteamManifest(string gamePath, int steamAppId, GameVersionInfo info)
    {
        if (steamAppId <= 0)
        {
            return false;
        }
        string fileName = $"appmanifest_{steamAppId}.acf";
        string? parent = Directory.GetParent(gamePath)?.FullName;
        foreach (string path in new[] { Path.Combine(gamePath, fileName), parent == null ? string.Empty : Path.Combine(parent, fileName) })
        {
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                Match match = Regex.Match(File.ReadAllText(path), "\\\"buildid\\\"\\s+\\\"(\\d+)\\\"");
                if (match.Success)
                {
                    info.VersionString = $"Steam Build {match.Groups[1].Value}";
                    return true;
                }
            }
            catch
            {
            }
        }
        return false;
    }

    private static bool TryDetectFromVersionFiles(string gamePath, IEnumerable<string> names, GameVersionInfo info)
    {
        string[] directories = [gamePath, Path.Combine(gamePath, "bin"), Path.Combine(gamePath, "data"), Path.Combine(gamePath, "config")];
        foreach (string directory in directories.Where(Directory.Exists))
        {
            foreach (string name in names.Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                string path = Path.Combine(directory, name);
                if (!File.Exists(path))
                {
                    continue;
                }
                try
                {
                    string value = File.ReadAllText(path).Trim();
                    if (value.Length > 0)
                    {
                        SetVersion(info, value);
                        return true;
                    }
                }
                catch
                {
                }
            }
        }
        return false;
    }

    private static void SetVersion(GameVersionInfo info, string value)
    {
        info.VersionString = value;
        if (Version.TryParse(value, out Version? parsed))
        {
            info.Version = parsed;
        }
    }

    private static void CheckVersionCompatibility(GameVersionInfo info)
    {
        if (ProblematicVersions.TryGetValue(info.VersionString, out string? warning))
        {
            info.IsProblematicVersion = true;
            info.IsKnownVersion = true;
            info.WarningMessage = warning;
        }
        else if (KnownGoodVersions.Contains(info.VersionString))
        {
            info.IsKnownVersion = true;
        }
        else
        {
            info.WarningMessage = "Unknown game version detected. Verify compatibility before modifying textures.";
        }
    }

    public string GetVersionLogString(GameVersionInfo info)
    {
        var text = new StringBuilder();
        text.AppendLine("=== Game Version Information ===");
        text.AppendLine($"  Version: {info.VersionString}");
        text.AppendLine($"  Parsed Version: {(info.Version != null ? info.Version.ToString() : "N/A")}");
        text.AppendLine($"  Platform: {info.DistributionPlatform}");
        text.AppendLine($"  Game Path: {info.GamePath}");
        text.AppendLine($"  Executable Date: {(info.ExecutableDate.HasValue ? info.ExecutableDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "N/A")}");
        text.AppendLine($"  Known Version: {info.IsKnownVersion}");
        text.AppendLine($"  Problematic: {info.IsProblematicVersion}");
        if (!string.IsNullOrEmpty(info.WarningMessage))
        {
            text.AppendLine($"  Warning: {info.WarningMessage}");
        }
        text.AppendLine("=== End Version Information ===");
        return text.ToString();
    }
}
