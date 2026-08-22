using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SpiderManModdingTool.Core;
using SpiderManModdingTool.Core.Games;

namespace SpiderManModdingTool.CLI
{
    class Program
    {
        static int Main(string[] args)
        {
            Logger.Initialize();
            Logger.CleanOldLogs();
            Logger.LogInfo($"CLI started with args: {string.Join(" ", args)}");

            try
            {
                if (args.Length == 0)
                {
                    ShowHelp();
                    return 1;
                }

                string command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "extract":
                        return HandleExtract(args.Skip(1).ToArray());
                    case "rebuild":
                        return HandleRebuild(args.Skip(1).ToArray());
                    case "version":
                        return HandleVersion();
                    case "help":
                        ShowHelp();
                        return 0;
                    case "list":
                        return HandleList(args.Skip(1).ToArray());
                    case "backup":
                        return HandleBackup(args.Skip(1).ToArray());
                    case "restore":
                        return HandleRestore(args.Skip(1).ToArray());
                    default:
                        Console.Error.WriteLine($"Unknown command: {command}");
                        ShowHelp();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "CLI Main");
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine($"Error code: {ErrorHandler.FromException(ex).Code}");
                return 1;
            }
        }

        static int HandleExtract(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: smtcli extract <texture-name> <output-png-path>");
                Console.Error.WriteLine("Example: smtcli extract suit_red_red C:\\textures\\suit_red.png");
                return 1;
            }

            string textureName = args[0];
            string outputPngPath = args[1];

            var resolved = ResolveGame();
            if (resolved == null) return 1;
            var (gamePath, game) = resolved.Value;

            Console.WriteLine($"Extracting texture '{textureName}' from game at '{gamePath}'...");
            WarnIfProblematicVersion(gamePath, game);
            
            var archiveManager = new ArchiveManager(game);
            bool success = archiveManager.ExtractTextureToPng(gamePath, textureName, outputPngPath);
            
            if (success)
            {
                Console.WriteLine($"Successfully extracted texture to: {outputPngPath}");
                return 0;
            }
            else
            {
                Console.Error.WriteLine("Failed to extract texture. The texture may not exist or be in an unsupported format.");
                return 1;
            }
        }

        static int HandleRebuild(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: smtcli rebuild <input-png-path> <texture-name> [--no-backup]");
                Console.Error.WriteLine("Example: smtcli rebuild C:\\textures\\suit_red_modified.png suit_red_red");
                return 1;
            }

            string inputPngPath = args[0];
            string textureName = args[1];
            bool createBackup = !args.Contains("--no-backup", StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(inputPngPath))
            {
                var error = ErrorHandler.FileNotFound(inputPngPath, "PNG file");
                Console.Error.WriteLine($"Error: {error.UserMessage}");
                return 1;
            }

            if (!PngValidator.ValidateForRebuild(inputPngPath, out ToolError? validationError))
            {
                Logger.LogError($"PNG validation failed: {validationError!.Code} - {inputPngPath}");
                Console.Error.WriteLine($"Error: {validationError.UserMessage}");
                return 1;
            }

            var resolved = ResolveGame();
            if (resolved == null) return 1;
            var (gamePath, game) = resolved.Value;

            Console.WriteLine($"Rebuilding texture '{textureName}' from PNG '{inputPngPath}'...");
            WarnIfProblematicVersion(gamePath, game);
            
            var archiveManager = new ArchiveManager(game);
            bool success = archiveManager.RebuildTextureFromPng(gamePath, textureName, inputPngPath, createBackup);
            
            if (success)
            {
                Console.WriteLine("Successfully rebuilt texture.");
                if (createBackup)
                {
                    Console.WriteLine("Backup created (use 'smtcli backup list' to view backups)");
                }
                return 0;
            }
            else
            {
                Console.Error.WriteLine("Failed to rebuild texture. The PNG may be invalid or the texture may not exist.");
                return 1;
            }
        }

        static int HandleVersion()
        {
            Console.WriteLine("Spider-Man Modding Tool CLI");
            Console.WriteLine("Version: 1.0.0");
            Console.WriteLine("© 2026 Spider-Man Modding Tool Project");
            Console.WriteLine();

            // Try to detect and show game version
            string? gamePath = DetectGamePath();
            if (gamePath != null)
            {
                try
                {
                    GameBase game = GameFactory.CreateGameFromPath(gamePath);
                    var detector = new GameVersionDetector();
                    var versionInfo = detector.DetectVersion(gamePath, game);
                    Console.WriteLine($"Game version: {versionInfo.VersionString} ({versionInfo.DistributionPlatform})");
                    if (!string.IsNullOrEmpty(versionInfo.WarningMessage))
                    {
                        Console.WriteLine($"Warning: {versionInfo.WarningMessage}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Game installation detected at '{gamePath}' but could not determine profile: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Game installation not detected. Set SPIDERMAN_GAME_PATH to check game version.");
            }

            return 0;
        }

        static int HandleList(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: smtcli list <texture-name>");
                Console.Error.WriteLine("Example: smtcli list suit_red_red");
                return 1;
            }

            string textureName = args[0];
            var resolved = ResolveGame();
            if (resolved == null) return 1;
            var (gamePath, game) = resolved.Value;

            Console.WriteLine($"Listing textures matching '{textureName}' in game at '{gamePath}'...");
            
            var archiveManager = new ArchiveManager(game);
            List<string> textures = archiveManager.GetTextureNames(gamePath);
            
            var matches = textures.Where(t => t.Contains(textureName, StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (matches.Count == 0)
            {
                Console.WriteLine("No matching textures found.");
                return 0;
            }

            Console.WriteLine($"\nFound {matches.Count} matching texture(s):");
            foreach (var texture in matches)
            {
                Console.WriteLine($"  {texture}");
            }
            
            return 0;
        }

        static int HandleBackup(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: smtcli backup <list|create> [texture-name]");
                Console.Error.WriteLine("  smtcli backup list [texture-name]     - List backups for a texture (or all if omitted)");
                Console.Error.WriteLine("  smtcli backup create <texture-name>   - Create manual backup of texture");
                return 1;
            }

            string subcommand = args[0].ToLowerInvariant();
            var resolved = ResolveGame();
            if (resolved == null) return 1;
            var (gamePath, game) = resolved.Value;

            var archiveManager = new ArchiveManager(game);

            switch (subcommand)
            {
                case "list":
                    return HandleBackupList(args.Skip(1).ToArray(), gamePath, archiveManager);
                case "create":
                    return HandleBackupCreate(args.Skip(1).ToArray(), gamePath, game, archiveManager);
                default:
                    Console.Error.WriteLine($"Unknown backup subcommand: {subcommand}");
                    return 1;
            }
        }

        static int HandleBackupList(string[] args, string gamePath, ArchiveManager archiveManager)
        {
            string? textureName = args.Length > 0 ? args[0] : null;
            
            if (textureName == null)
            {
                // List all textures with backups
                List<string> allTextures = archiveManager.GetTextureNames(gamePath);
                Console.WriteLine($"Available textures in game: {allTextures.Count}");
                Console.WriteLine();
                
                foreach (string texture in allTextures)
                {
                    List<BackupInfo> backups = archiveManager.GetBackupInfo(gamePath, texture);
                    if (backups.Count > 0)
                    {
                        Console.WriteLine($"{texture}: {backups.Count} backup(s)");
                        foreach (var backup in backups.OrderByDescending(b => b.Timestamp))
                        {
                            Console.WriteLine($"  {backup.Timestamp:yyyy-MM-dd HH:mm:ss} ({backup.FilePath})");
                        }
                        Console.WriteLine();
                    }
                }
            }
            else
            {
                // List backups for specific texture
                List<BackupInfo> backups = archiveManager.GetBackupInfo(gamePath, textureName);
                if (backups.Count == 0)
                {
                    Console.WriteLine($"No backups found for texture: {textureName}");
                    return 0;
                }

                Console.WriteLine($"Backups for texture '{textureName}':");
                foreach (var backup in backups.OrderByDescending(b => b.Timestamp))
                {
                    Console.WriteLine($"{backup.Timestamp:yyyy-MM-dd HH:mm:ss} ({backup.FilePath})");
                }
            }
            
            return 0;
        }

        static int HandleBackupCreate(string[] args, string gamePath, GameBase game, ArchiveManager archiveManager)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: smtcli backup create <texture-name>");
                return 1;
            }

            string textureName = args[0];
            
            Console.WriteLine($"Creating backup of texture '{textureName}'...");
            
            // For manual backup, we need to find the texture and copy it
            // This is a simplified version - in reality we'd want to use the same backup mechanism as rebuild
            List<string> textures = archiveManager.GetTextureNames(gamePath);
            if (!textures.Contains(textureName, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Error: Texture '{textureName}' not found in game.");
                return 1;
            }

            // Find the actual texture name (preserving case)
            string actualTextureName = textures.First(t => t.Equals(textureName, StringComparison.OrdinalIgnoreCase));
            
            // Get the backup directory
            string backupDir = Path.Combine(gamePath, archiveManager.BackupDirectoryRelativePath);
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            // Find texture in TOC to get offset and size
            string tocPath = Path.Combine(gamePath, game.ArchiveDirectory, game.TocFileName);
            long textureOffset = -1;
            int textureSize = 0;

            using (BinaryReader reader = new BinaryReader(File.OpenRead(tocPath)))
            {
                reader.BaseStream.Seek(0x20, SeekOrigin.Begin);
                int entryCount = reader.ReadInt32();
                reader.BaseStream.Seek(0x28, SeekOrigin.Begin);

                for (int i = 0; i < entryCount; i++)
                {
                    long offset = reader.ReadInt64();
                    int size = reader.ReadInt32();
                    int nameOffset = reader.ReadInt32();
                    int nameLength = reader.ReadInt32();

                    if (nameOffset > 0 && nameLength > 0)
                    {
                        long currentPos = reader.BaseStream.Position;
                        reader.BaseStream.Seek(nameOffset, SeekOrigin.Begin);

                        List<byte> nameBytes = new List<byte>();
                        byte b;
                        while ((b = reader.ReadByte()) != 0 && nameBytes.Count < nameLength)
                        {
                            nameBytes.Add(b);
                        }

                        string name = Encoding.ASCII.GetString(nameBytes.ToArray());

                        if (string.Equals(name, actualTextureName + ".texture", StringComparison.OrdinalIgnoreCase))
                        {
                            textureOffset = offset;
                            textureSize = size;
                            break;
                        }

                        reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
                    }

                    reader.BaseStream.Seek(0x18, SeekOrigin.Current);
                }
            }

            if (textureOffset == -1 || textureSize == 0)
            {
                Console.Error.WriteLine($"Error: Could not locate texture '{actualTextureName}.texture' in game archives.");
                return 1;
            }

            // Read texture data
            string archiveFilePath = Path.Combine(gamePath, game.ArchiveDirectory, actualTextureName + ".g00s000");
            byte[] textureData = new byte[textureSize];
            
            using (FileStream fs = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read))
            {
                fs.Seek(textureOffset, SeekOrigin.Begin);
                int bytesRead = fs.Read(textureData, 0, textureSize);
                if (bytesRead != textureSize)
                {
                    Console.Error.WriteLine($"Error: Failed to read complete texture data.");
                    return 1;
                }
            }

            // Create backup file
            string backupFilePath = Path.Combine(backupDir, $"{actualTextureName}_{DateTime.Now:yyyyMMdd_HHmmss}.texture.bak");
            File.WriteAllBytes(backupFilePath, textureData);
            
            // Enforce retention policy
            archiveManager.EnforceBackupRetentionPolicy(backupDir, actualTextureName);
            
            Console.WriteLine($"Backup created: {backupFilePath}");
            return 0;
        }

        static int HandleRestore(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: smtcli restore <texture-name>");
                Console.Error.WriteLine("Example: smtcli restore suit_red_red");
                return 1;
            }

            string textureName = args[0];
            var resolved = ResolveGame();
            if (resolved == null) return 1;
            var (gamePath, game) = resolved.Value;

            Console.WriteLine($"Restoring texture '{textureName}' from most recent backup...");
            
            var archiveManager = new ArchiveManager(game);
            bool success = archiveManager.RestoreTextureFromBackup(gamePath, textureName);
            
            if (success)
            {
                Console.WriteLine("Successfully restored texture from backup.");
                return 0;
            }
            else
            {
                Console.Error.WriteLine("Failed to restore texture. No backups found or restore operation failed.");
                return 1;
            }
        }

        static void WarnIfProblematicVersion(string gamePath, GameBase game)
        {
            try
            {
                var detector = new GameVersionDetector();
                var versionInfo = detector.DetectVersion(gamePath, game);
                if (versionInfo.IsProblematicVersion && !string.IsNullOrEmpty(versionInfo.WarningMessage))
                {
                    Console.Error.WriteLine($"Warning: {versionInfo.WarningMessage}");
                }
                else if (!versionInfo.IsKnownVersion && versionInfo.VersionString != "Unknown")
                {
                    Console.Error.WriteLine($"Note: Unknown game version '{versionInfo.VersionString}'. Use with caution.");
                }
            }
            catch
            {
                // Silently ignore version detection errors
            }
        }

        static (string gamePath, GameBase game)? ResolveGame()
        {
            string? gamePath = DetectGamePath();
            if (gamePath == null)
            {
                Console.Error.WriteLine("Error: Could not auto-detect game installation. Please specify game path via environment variable SPIDERMAN_GAME_PATH");
                return null;
            }
            try
            {
                GameBase game = GameFactory.CreateGameFromPath(gamePath);
                return (gamePath, game);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }

        static string? DetectGamePath()
        {
            // Check environment variable first
            string? envPath = Environment.GetEnvironmentVariable("SPIDERMAN_GAME_PATH");
            if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            {
                return envPath;
            }

            // Common installation paths
            string[] commonPaths = 
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Spider-Man Remastered"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Spider-Man Remastered"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games", "SpiderManRemastered"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games", "SpiderManRemastered"),
                @"C:\Program Files (x86)\Steam\steamapps\common\Spider-Man Remastered",
                @"C:\Program Files\Steam\steamapps\common\Spider-Man Remastered",
                @"C:\Program Files (x86)\Epic Games\SpiderManRemastered",
                @"C:\Program Files\Epic Games\SpiderManRemastered"
            };

            foreach (string path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        static void ShowHelp()
        {
            Console.WriteLine("Spider-Man Modding Tool CLI (smtcli)");
            Console.WriteLine();
            Console.WriteLine("Usage: smtcli <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  extract <texture-name> <output-png>     Extract texture to PNG file");
            Console.WriteLine("  rebuild <input-png> <texture-name>      Rebuild texture from PNG file (--no-backup to skip backup)");
            Console.WriteLine("  list <texture-name>                     List textures matching name");
            Console.WriteLine("  backup list [texture-name]              List backups (for texture or all)");
            Console.WriteLine("  backup create <texture-name>            Create manual backup of texture");
            Console.WriteLine("  restore <texture-name>                  Restore texture from most recent backup");
            Console.WriteLine("  version                                 Show version information");
            Console.WriteLine("  help                                    Show this help message");
            Console.WriteLine();
            Console.WriteLine("Environment Variables:");
            Console.WriteLine("  SPIDERMAN_GAME_PATH   Override auto-detection of game installation path");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  smtcli list suit");
            Console.WriteLine("  smtcli extract suit_red_red C:\\textures\\suit_red.png");
            Console.WriteLine("  smtcli rebuild C:\\textures\\suit_red_modified.png suit_red_red");
            Console.WriteLine("  smtcli backup list suit_red_red");
            Console.WriteLine("  smtcli restore suit_red_red");
        }
    }
}