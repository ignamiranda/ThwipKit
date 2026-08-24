using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ThwipKit.Core
{
    public class AppSettings
    {
        public string GamePath { get; set; } = "";
        public string ImageEditorPath { get; set; } = "";
        public string BackupDirectory { get; set; } = "backups";
        public bool EnableBackups { get; set; } = true;
        public int MaxBackupFiles { get; set; } = 10;
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public int WindowWidth { get; set; } = 800;
        public int WindowHeight { get; set; } = 450;
        public bool WindowMaximized { get; set; }
        public List<string> RecentTextures { get; set; } = new();
        public string LastExtractDirectory { get; set; } = "";
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThwipKit");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");
        private static readonly string BackupFilePath = Path.Combine(SettingsDirectory, "settings.json.bak");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        Logger.LogInfo("Settings loaded successfully");
                        return settings;
                    }
                }

                if (File.Exists(BackupFilePath))
                {
                    Logger.LogWarning("Settings file corrupt, loading backup");
                    string json = File.ReadAllText(BackupFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "LoadSettings");
            }

            Logger.LogInfo("Using default settings");
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                if (File.Exists(SettingsFilePath))
                {
                    File.Copy(SettingsFilePath, BackupFilePath, overwrite: true);
                }

                string json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
                Logger.LogInfo("Settings saved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SaveSettings");
            }
        }

        public void AddRecentTexture(string textureName)
        {
            RecentTextures.Remove(textureName);
            RecentTextures.Insert(0, textureName);
            if (RecentTextures.Count > 20)
            {
                RecentTextures.RemoveAt(RecentTextures.Count - 1);
            }
        }

        public static string GetSettingsFilePath() => SettingsFilePath;
        public static string GetSettingsDirectory() => SettingsDirectory;
    }

    public static class TempFileManager
    {
        private static readonly List<string> TrackedFiles = new();
        private static readonly string TempDirectory = Path.Combine(
            Path.GetTempPath(), "ThwipKit");
        private static readonly object Lock = new();

        public static string CreateTempFile(string extension = ".tmp")
        {
            lock (Lock)
            {
                if (!Directory.Exists(TempDirectory))
                {
                    Directory.CreateDirectory(TempDirectory);
                }

                string fileName = $"smt_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}";
                string filePath = Path.Combine(TempDirectory, fileName);

                File.Create(filePath).Close();
                TrackedFiles.Add(filePath);
                Logger.LogDebug($"Created temp file: {filePath}");
                return filePath;
            }
        }

        public static string CreateTempFile(string prefix, string extension = ".tmp")
        {
            lock (Lock)
            {
                if (!Directory.Exists(TempDirectory))
                {
                    Directory.CreateDirectory(TempDirectory);
                }

                string fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}";
                string filePath = Path.Combine(TempDirectory, fileName);

                File.Create(filePath).Close();
                TrackedFiles.Add(filePath);
                return filePath;
            }
        }

        public static void TrackFile(string filePath)
        {
            lock (Lock)
            {
                if (!TrackedFiles.Contains(filePath))
                {
                    TrackedFiles.Add(filePath);
                }
            }
        }

        public static void CleanupAll()
        {
            lock (Lock)
            {
                Logger.LogInfo($"Cleaning up {TrackedFiles.Count} temp files");

                foreach (string filePath in TrackedFiles.ToArray())
                {
                    DeleteFileSecurely(filePath);
                }

                TrackedFiles.Clear();

                if (Directory.Exists(TempDirectory))
                {
                    try
                    {
                        foreach (string file in Directory.GetFiles(TempDirectory))
                        {
                            DeleteFileSecurely(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "CleanupAll directory");
                    }
                }
            }
        }

        public static void CleanupFile(string filePath)
        {
            lock (Lock)
            {
                DeleteFileSecurely(filePath);
                TrackedFiles.Remove(filePath);
            }
        }

        private static void DeleteFileSecurely(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                FileInfo info = new(filePath);
                if (info.Length > 0)
                {
                    try
                    {
                        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write);
                        byte[] zeros = new byte[Math.Min(info.Length, 4096)];
                        stream.Write(zeros, 0, zeros.Length);
                        stream.Flush();
                    }
                    catch
                    {
                        // If secure overwrite fails, proceed with normal deletion
                    }
                }

                File.Delete(filePath);
                Logger.LogDebug($"Deleted temp file: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"DeleteFileSecurely: {filePath}");
            }
        }

        public static int GetTrackedFileCount()
        {
            lock (Lock)
            {
                return TrackedFiles.Count;
            }
        }

        public static string GetTempDirectory() => TempDirectory;
    }
}