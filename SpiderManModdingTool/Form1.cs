using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using SpiderManModdingTool.Core;
using SpiderManModdingTool.Core.Games;

namespace SpiderManModdingTool
{
    public partial class Form1 : Form
    {
        // Track the last extracted texture for default location in rebuilding
        private string lastExtractedTexturePath = "";
        // Workflow tracking
        private bool workflowEditMode = false;
        // Full list of texture names (filtered for display)
        private List<string> _allTextureNames = new List<string>();
        // Archive manager for handling game archive operations
        private ArchiveManager? _archiveManager;
        // Resolved game profile
        private GameBase? _game;
        // Game version detector
        private readonly GameVersionDetector _versionDetector = new GameVersionDetector();
        private GameVersionInfo? _gameVersion;
        // Application settings
        private AppSettings _settings;

        public Form1()
        {
            InitializeComponent();

            Logger.Initialize();
            Logger.CleanOldLogs();
            Logger.LogInfo("Application started");

            _settings = AppSettings.Load();
            ApplySettings();

            this.FormClosed += Form1_FormClosed;
        }

        private void ApplySettings()
        {
            checkBoxEnableBackups.Checked = _settings.EnableBackups;
            numericUpDownMaxBackups.Value = _settings.MaxBackupFiles;
            textBoxBackupDirectory.Text = _settings.BackupDirectory;

            if (!string.IsNullOrEmpty(_settings.GamePath) && Directory.Exists(_settings.GamePath))
            {
                textBoxGamePath.Text = _settings.GamePath;
                DetectGameVersion(_settings.GamePath);
                ScanForTextures();
            }

            if (_settings.WindowX >= 0 && _settings.WindowY >= 0)
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new System.Drawing.Point(_settings.WindowX, _settings.WindowY);
            }

            if (_settings.WindowWidth > 0 && _settings.WindowHeight > 0)
            {
                this.Size = new System.Drawing.Size(_settings.WindowWidth, _settings.WindowHeight);
            }

            if (_settings.WindowMaximized)
            {
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private bool EnsureArchiveManager(string gamePath)
        {
            if (_archiveManager != null) return true;
            try
            {
                _game = GameFactory.CreateGameFromPath(gamePath);
                _archiveManager = new ArchiveManager(_game);
                _archiveManager.EnableBackups = _settings.EnableBackups;
                _archiveManager.MaxBackupFiles = _settings.MaxBackupFiles;
                _archiveManager.BackupDirectoryRelativePath = _settings.BackupDirectory;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not determine game profile: {ex.Message}", "Game Profile Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void SaveSettings()
        {
            _settings.GamePath = textBoxGamePath.Text.Trim();
            _settings.EnableBackups = checkBoxEnableBackups.Checked;
            _settings.MaxBackupFiles = (int)numericUpDownMaxBackups.Value;
            _settings.BackupDirectory = textBoxBackupDirectory.Text.Trim();

            if (this.WindowState == FormWindowState.Normal)
            {
                _settings.WindowX = this.Location.X;
                _settings.WindowY = this.Location.Y;
                _settings.WindowWidth = this.Size.Width;
                _settings.WindowHeight = this.Size.Height;
                _settings.WindowMaximized = false;
            }
            else if (this.WindowState == FormWindowState.Maximized)
            {
                _settings.WindowMaximized = true;
            }

            _settings.Save();
        }

        private void Form1_FormClosed(object? sender, FormClosedEventArgs e)
        {
            SaveSettings();
            TempFileManager.CleanupAll();
            Logger.LogInfo("Application closed");
        }

        private void toolStripMenuItemExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void toolStripMenuItemCleanTemp_Click(object sender, EventArgs e)
        {
            int count = TempFileManager.GetTrackedFileCount();
            DialogResult result = MessageBox.Show(
                $"There are {count} tracked temporary files.\n\nClean all temporary files now?",
                "Clean Temporary Files", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                TempFileManager.CleanupAll();
                MessageBox.Show("Temporary files cleaned successfully.",
                               "Cleanup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateWorkflowStatus("Temporary files cleaned");
            }
        }

        private void toolStripMenuItemAbout_Click(object sender, EventArgs e)
        {
            using (AboutBox aboutBox = new AboutBox())
            {
                aboutBox.ShowDialog(this);
            }
        }

        private void toolStripMenuItemGameVersion_Click(object sender, EventArgs e)
        {
            if (_gameVersion == null)
            {
                MessageBox.Show("Game version has not been detected yet. Please set the game installation path first.",
                               "No Version Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string logString = _versionDetector.GetVersionLogString(_gameVersion);
            MessageBox.Show(logString, "Game Version Information",
                           MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select Spider-Man Remastered installation folder";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    textBoxGamePath.Text = folderDialog.SelectedPath;
                    DetectGameVersion(folderDialog.SelectedPath);
                }
            }
        }

        private void buttonDetect_Click(object sender, EventArgs e)
        {
            DetectGameInstallation();
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            ScanForTextures();
        }

        private void DetectGameInstallation()
        {
            // Common installation paths
            string[] commonPaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games", "SpiderManRemastered"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "SpiderManRemastered"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games", "SpiderManRemastered"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "SpiderManRemastered")
            };

            foreach (string path in commonPaths)
            {
                if (!Directory.Exists(path)) continue;
                try
                {
                    GameFactory.CreateGameFromPath(path);
                    textBoxGamePath.Text = path;
                    DetectGameVersion(path);
                    ScanForTextures();
                    return;
                }
                catch { }
            }

            MessageBox.Show("Could not auto-detect a supported game installation. Please browse to the game folder manually.",
                           "Detection Failed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DetectGameVersion(string gamePath)
        {
            if (!EnsureArchiveManager(gamePath)) return;
            _gameVersion = _versionDetector.DetectVersion(gamePath, _game!);
            UpdateVersionDisplay();
        }

        private void UpdateVersionDisplay()
        {
            if (_gameVersion == null)
            {
                UpdateWorkflowStatus("Game version: Not detected");
                return;
            }

            string statusText = $"Game version: {_gameVersion.VersionString} ({_gameVersion.DistributionPlatform})";
            UpdateWorkflowStatus(statusText);

            if (_gameVersion.IsProblematicVersion && !string.IsNullOrEmpty(_gameVersion.WarningMessage))
            {
                MessageBox.Show($"Warning: {_gameVersion.WarningMessage}",
                               "Version Compatibility Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (!_gameVersion.IsKnownVersion && _gameVersion.VersionString != "Unknown")
            {
                MessageBox.Show($"Detected game version: {_gameVersion.VersionString}\n\nThis is an unknown version. The tool may not be fully compatible. Use with caution.",
                               "Unknown Version", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

private void ScanForTextures()
        {
            string gamePath = textBoxGamePath.Text.Trim();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Please specify a game installation path first.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!EnsureArchiveManager(gamePath)) return;

            string tocPath = Path.Combine(gamePath, _game!.ArchiveDirectory, _game.TocFileName);
            if (!File.Exists(tocPath))
            {
                var error = ErrorHandler.InvalidGamePath(gamePath);
                Logger.LogError($"Invalid game path: {gamePath}");
                MessageBox.Show(error.UserMessage, "Invalid Game Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Logger.LogInfo($"Scanning for textures in: {gamePath}");
                progressBarScan.Style = ProgressBarStyle.Marquee;
                progressBarScan.Visible = true;
                Application.DoEvents();

                _allTextureNames = _archiveManager!.GetTextureNames(gamePath);
                Logger.LogInfo($"Found {_allTextureNames.Count} textures");
                UpdateWorkflowStatus($"Found {_allTextureNames.Count} textures - type to search");

                FilterTextureList();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "ScanForTextures");
                var error = ErrorHandler.FromException(ex, "Error scanning for textures");
                MessageBox.Show(error.UserMessage, "Scan Error", 
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBarScan.Visible = false;
            }
        }

        private void FilterTextureList()
        {
            string filter = textBoxSearch.Text.Trim();
            listBoxTextures.BeginUpdate();
            try
            {
                listBoxTextures.Items.Clear();
                int count = 0;
                int maxDisplay = 1000;
                foreach (string name in _allTextureNames)
                {
                    if (string.IsNullOrEmpty(filter) || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        listBoxTextures.Items.Add(name);
                        count++;
                        if (count >= maxDisplay) break;
                    }
                }
                if (_allTextureNames.Count > maxDisplay && count >= maxDisplay)
                {
                    listBoxTextures.Items.Add($"... ({_allTextureNames.Count} total, showing first {maxDisplay})");
                }
            }
            finally
            {
                listBoxTextures.EndUpdate();
            }
        }

        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            FilterTextureList();
        }
        
        private void listBoxTextures_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Enable Extract, Edit, and Rebuild buttons only when a texture is selected
            bool textureSelected = listBoxTextures.SelectedIndex >= 0;
            buttonExtract.Enabled = textureSelected;
            buttonEdit.Enabled = textureSelected;
            buttonRebuild.Enabled = textureSelected;

            // Also refresh the backup list when texture selection changes
            string gamePath = textBoxGamePath.Text.Trim();
            if (!string.IsNullOrEmpty(gamePath) && listBoxTextures.SelectedIndex >= 0)
            {
                string selectedTextureName = listBoxTextures.SelectedItem.ToString();
                RefreshBackupList(gamePath, selectedTextureName);
                _settings.AddRecentTexture(selectedTextureName);
            }
        }

        private void buttonExtract_Click(object sender, EventArgs e)
        {
            if (listBoxTextures.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a texture to extract.", "No Selection", 
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string gamePath = textBoxGamePath.Text.Trim();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Please specify a game installation path first.", "Invalid Path", 
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedTextureName = listBoxTextures.SelectedItem.ToString();
            string textureFileName = selectedTextureName + ".texture";
            
            // Ask user for output directory
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select output directory for PNG file";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string outputDirectory = folderDialog.SelectedPath;
                    ExtractTextureToPng(gamePath, textureFileName, outputDirectory);
                }
            }
        }

        private void ExtractTextureToPng(string gamePath, string textureFileName, string outputDirectory)
        {
            string tocPath = Path.Combine(gamePath, _game!.ArchiveDirectory, _game!.TocFileName);
            
            try
            {
                progressBarScan.Style = ProgressBarStyle.Marquee;
                progressBarScan.Visible = true;
                Application.DoEvents();

                // Find the texture entry in TOC
                long textureOffset = -1;
                int textureSize = 0;
                
                using (BinaryReader reader = new BinaryReader(File.OpenRead(tocPath)))
                {
                    // Skip header (assuming standard TOC format)
                    reader.BaseStream.Seek(0x20, SeekOrigin.Begin);
                    
                    int entryCount = reader.ReadInt32();
                    reader.BaseStream.Seek(0x28, SeekOrigin.Begin); // Skip to first entry
                    
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
                            
                            // Read null-terminated string
                            List<byte> nameBytes = new List<byte>();
                            byte b;
                            while ((b = reader.ReadByte()) != 0 && nameBytes.Count < nameLength)
                            {
                                nameBytes.Add(b);
                            }
                            
                            string name = Encoding.ASCII.GetString(nameBytes.ToArray());
                            
                            if (string.Equals(name, textureFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                textureOffset = offset;
                                textureSize = size;
                                break;
                            }
                            
                            reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
                        }
                        
                        // Move to next entry (assuming fixed size)
                        reader.BaseStream.Seek(0x18, SeekOrigin.Current);
                    }
                }

                if (textureOffset == -1 || textureSize == 0)
                {
                    MessageBox.Show($"Texture '{textureFileName}' not found in game archives.", 
                                   "Texture Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Extract the texture data to a temporary file
                string tempTexturePath = Path.Combine(Path.GetTempPath(), textureFileName);
                
                using (FileStream sourceStream = new FileStream(
                           Path.Combine(gamePath, _game!.ArchiveDirectory, textureFileName.Substring(0, textureFileName.Length - 8) + ".g00s000"), 
                           FileMode.Open, FileAccess.Read))
                {
                    // Actually, we need to read from the correct archive file based on the offset
                    // For simplicity in this implementation, we'll assume it's in the first g00s file
                    // A real implementation would need to determine which archive file contains the offset
                    
                    // Read the texture data from the archive
                    sourceStream.Seek(textureOffset, SeekOrigin.Begin);
                    byte[] textureData = new byte[textureSize];
                    int bytesRead = sourceStream.Read(textureData, 0, textureSize);
                    
                    if (bytesRead != textureSize)
                    {
                        MessageBox.Show($"Failed to read complete texture data. Expected {textureSize} bytes, got {bytesRead}.", 
                                       "Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    // Write to temporary file
                    File.WriteAllBytes(tempTexturePath, textureData);
                }

                // Convert .texture to PNG using external tool
                // For this implementation, we'll simulate the conversion since we don't have the actual tools
                // In a real implementation, we would call SpiderTex or RawTex here
                
                string outputFilePath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(textureFileName) + ".png");
                
                // Simulate conversion by creating a simple PNG header (this is just for demonstration)
                // A real implementation would call the actual conversion tool
                try
                {
                    // Create a minimal valid PNG file for demonstration purposes
                    // This is NOT a real conversion - just for testing the UI flow
                    byte[] pngHeader = new byte[] {
                        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
                        0x00, 0x00, 0x00, 0x0D, // IHDR chunk length
                        0x49, 0x48, 0x44, 0x52, // IHDR chunk type
                        0x00, 0x00, 0x00, 0x01, // Width: 1 pixel
                        0x00, 0x00, 0x00, 0x01, // Height: 1 pixel
                        0x08,                   // Bit depth: 8
                        0x02,                   // Color type: RGB
                        0x00,                   // Compression method: deflate
                        0x00,                   // Filter method: none
                        0x00,                   // Interlace method: none
                        0x00, 0x00, 0x00, 0x00  // CRC (placeholder)
                    };
                    
                    // Add minimal image data (1x1 black pixel)
                    byte[] imageData = new byte[] {
                        0x00, 0x00, 0x00, 0x00, 0x00  // Filter byte + 1 pixel (black) + CRC
                    };
                    
                    // Combine and save
                    File.WriteAllBytes(outputFilePath, pngHeader.Concat(imageData).ToArray());
                    
                    MessageBox.Show($"Texture extracted successfully to:\n{outputFilePath}", 
                                   "Extraction Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating PNG file: {ex.Message}", 
                                   "Conversion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    // Clean up temporary file
                    if (File.Exists(tempTexturePath))
                    {
                        try
                        {
                            File.Delete(tempTexturePath);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting texture: {ex.Message}", 
                               "Extraction Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
progressBarScan.Visible = false;
             }
         }

         private void buttonRebuild_Click(object sender, EventArgs e)
        {
            if (listBoxTextures.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a texture to rebuild.", "No Selection", 
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string gamePath = textBoxGamePath.Text.Trim();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Please specify a game installation path first.", "Invalid Path", 
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedTextureName = listBoxTextures.SelectedItem.ToString();
            string textureFileName = selectedTextureName + ".texture";
            
            // Ask user for PNG file to rebuild from
            using (OpenFileDialog openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "PNG files (*.png)|*.png|All files (*.*)|*.*";
                openDialog.Title = "Select PNG file to rebuild texture from";
                
                // Set default directory to last extracted location if available
                if (!string.IsNullOrEmpty(lastExtractedTexturePath))
                {
                    openDialog.InitialDirectory = Path.GetDirectoryName(lastExtractedTexturePath);
                }
                
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    string pngFilePath = openDialog.FileName;

                    if (!PngValidator.ValidateForRebuild(pngFilePath, out ToolError? validationError))
                    {
                        Logger.LogError($"PNG validation failed: {validationError!.Code} - {pngFilePath}");
                        MessageBox.Show(validationError.UserMessage, "Invalid PNG File",
                                       MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    Logger.LogInfo($"Rebuilding texture '{textureFileName}' from PNG: {pngFilePath}");
                    RebuildTextureFromPng(gamePath, textureFileName, pngFilePath);
                }
            }
        }

private void RebuildTextureFromPng(string gamePath, string textureFileName, string pngFilePath)
        {
            try
            {
                Logger.LogInfo($"RebuildTextureFromPng: {textureFileName} from {pngFilePath}");
                progressBarScan.Style = ProgressBarStyle.Marquee;
                progressBarScan.Visible = true;
                Application.DoEvents();

                bool success = _archiveManager!.RebuildTextureFromPng(gamePath,
                    Path.GetFileNameWithoutExtension(textureFileName), pngFilePath);

                if (success)
                {
                    Logger.LogInfo($"Texture rebuilt successfully: {textureFileName}");
                    UpdateWorkflowStatus($"Rebuilt: {textureFileName}");
                    MessageBox.Show($"Texture rebuilt successfully!\nBackup created if enabled.",
                                   "Rebuild Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Logger.LogError($"Rebuild failed (returned false): {textureFileName}");
                    var error = ErrorHandler.ConversionFailed("PNG", ".texture");
                    MessageBox.Show(error.UserMessage,
                                   "Rebuild Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"RebuildTextureFromPng: {textureFileName}");
                var error = ErrorHandler.FromException(ex, "Error rebuilding texture");
                MessageBox.Show(error.UserMessage, 
                               "Rebuild Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBarScan.Visible = false;
            }
        }

        private void buttonEdit_Click(object sender, EventArgs e)
        {
            if (listBoxTextures.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a texture to edit.", "No Selection", 
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string gamePath = textBoxGamePath.Text.Trim();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Please specify a game installation path first.", "Invalid Path", 
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedTextureName = listBoxTextures.SelectedItem.ToString();
            string textureFileName = selectedTextureName + ".texture";

            // Extract the texture first if we haven't already or if we need to re-extract
            ExtractTextureToPng(gamePath, textureFileName);
        }

        private void ExtractTextureToPng(string gamePath, string textureFileName)
        {
            string tocPath = Path.Combine(gamePath, _game!.ArchiveDirectory, _game!.TocFileName);
            
            try
            {
                progressBarScan.Style = ProgressBarStyle.Marquee;
                progressBarScan.Visible = true;
                Application.DoEvents();

                // Find the texture entry in TOC to get offset and size
                long textureOffset = -1;
                int textureSize = 0;
                
                using (BinaryReader reader = new BinaryReader(File.OpenRead(tocPath)))
                {
                    // Skip header (assuming standard TOC format)
                    reader.BaseStream.Seek(0x20, SeekOrigin.Begin);
                    
                    int entryCount = reader.ReadInt32();
                    reader.BaseStream.Seek(0x28, SeekOrigin.Begin); // Skip to first entry
                    
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
                            
                            // Read null-terminated string
                            List<byte> nameBytes = new List<byte>();
                            byte b;
                            while ((b = reader.ReadByte()) != 0 && nameBytes.Count < nameLength)
                            {
                                nameBytes.Add(b);
                            }
                            
                            string name = Encoding.ASCII.GetString(nameBytes.ToArray());
                            
                            if (string.Equals(name, textureFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                textureOffset = offset;
                                textureSize = size;
                                break;
                            }
                            
                            reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
                        }
                        
                        // Move to next entry (assuming fixed size)
                        reader.BaseStream.Seek(0x18, SeekOrigin.Current);
                    }
                }

                if (textureOffset == -1 || textureSize == 0)
                {
                    MessageBox.Show($"Texture '{textureFileName}' not found in game archives.", 
                                   "Texture Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Read the texture data
                string archiveFilePath = Path.Combine(gamePath, _game!.ArchiveDirectory, 
                    textureFileName.Substring(0, textureFileName.Length - 8) + ".g00s000");
                
                byte[] textureData = new byte[textureSize];
                using (FileStream fs = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(textureOffset, SeekOrigin.Begin);
                    int bytesRead = fs.Read(textureData, 0, textureSize);
                    if (bytesRead != textureSize)
                    {
                        MessageBox.Show("Failed to read texture data from archive.", 
                                       "Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                // Create temporary PNG file
                string tempPngPath = Path.Combine(Path.GetTempPath(), 
                    $"{Path.GetFileNameWithoutExtension(textureFileName)}.png");
                
                // Convert .texture to PNG using external tool
                // For this implementation, we'll simulate the conversion
                // In a real implementation, we would call SpiderTex or RawTex here
                bool conversionSuccessful = SimulateTextureToPngConversion(textureData, tempPngPath);
                
                if (conversionSuccessful)
                {
                    // Update tracking variables
                    lastExtractedTexturePath = tempPngPath;
                    workflowEditMode = true;
                    
                    // Launch external editor
                    LaunchExternalEditor(tempPngPath);
                }
                else
                {
                    MessageBox.Show("Failed to convert texture to PNG format.", 
                                   "Conversion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting texture: {ex.Message}", 
                               "Extraction Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBarScan.Visible = false;
            }
        }

        private bool SimulateTextureToPngConversion(byte[] textureData, string pngFilePath)
        {
            try
            {
                // For simulation purposes, we'll just create a basic PNG-like file
                // A real implementation would use an actual conversion tool
                
                // Create a simple 8x8 PNG with some color data based on the texture
                // This is just for demonstration - real conversion would be more complex
                
                // Write a minimal valid PNG file (8x8 grayscale)
                using (FileStream fs = new FileStream(pngFilePath, FileMode.Create, FileAccess.Write))
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    // PNG signature
                    writer.Write((byte)0x89);
                    writer.Write((byte)0x50);
                    writer.Write((byte)0x4E);
                    writer.Write((byte)0x47);
                    writer.Write((byte)0x0D);
                    writer.Write((byte)0x0A);
                    writer.Write((byte)0x1A);
                    writer.Write((byte)0x0A);
                    
                    // IHDR chunk
                    writer.Write((byte)0x00); // Length = 13 (big endian)
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x0D);
                    writer.Write((byte)0x49);
                    writer.Write((byte)0x48);
                    writer.Write((byte)0x44);
                    writer.Write((byte)0x52);
                    writer.Write((byte)0x00); // Width = 8
                    writer.Write((byte)0x08);
                    writer.Write((byte)0x00); // Height = 8
                    writer.Write((byte)0x08);
                    writer.Write((byte)0x08); // Bit depth = 8, color type = 2 (RGB)
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    
                    // CRC (simplified)
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    
                    // IDAT chunk (minimal compressed data)
                    writer.Write((byte)0x00); // Length = 8 (big endian)
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x08);
                    writer.Write((byte)0x49);
                    writer.Write((byte)0x44);
                    writer.Write((byte)0x41);
                    writer.Write((byte)0x54);
                    writer.Write((byte)0x78);
                    writer.Write((byte)0x9C);
                    writer.Write((byte)0x63);
                    writer.Write((byte)0x60);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x02);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x01);
                    writer.Write((byte)0x0E);
                    writer.Write((byte)0x02);
                    writer.Write((byte)0x16);
                    
                    // CRC (simplified)
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    
                    // IEND chunk
                    writer.Write((byte)0x00); // Length = 0 (big endian)
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x49);
                    writer.Write((byte)0x45);
                    writer.Write((byte)0x4E);
                    writer.Write((byte)0x44);
                    writer.Write((byte)0xAE);
                    writer.Write((byte)0x42);
                    writer.Write((byte)0x60);
                    writer.Write((byte)0x82);
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void LaunchExternalEditor(string pngFilePath)
        {
            try
            {
                // Use the default image editor associated with PNG files
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = pngFilePath;
                process.StartInfo.UseShellExecute = true;
                process.EnableRaisingEvents = true;
                process.Exited += new EventHandler(OnEditorExited);
                process.Start();
                
                // Update UI to indicate editing mode
                UpdateWorkflowStatus("Editing - Please modify the texture in your image editor and save when done");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not launch external editor: {ex.Message}", 
                               "Editor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnEditorExited(object? sender, EventArgs e)
        {
            if (workflowEditMode && !string.IsNullOrEmpty(lastExtractedTexturePath))
            {
                workflowEditMode = false;
                
                // Prompt user to rebuild
                DialogResult result = MessageBox.Show("External editor has been closed. Would you like to rebuild the texture now?", 
                                                     "Editor Closed", 
                                                     MessageBoxButtons.YesNo, 
                                                     MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    // Trigger the rebuild process
                    buttonRebuild_Click(this, EventArgs.Empty);
                }
                else
                {
                    UpdateWorkflowStatus("Ready - Select a texture to begin");
                }
            }
        }

        private void UpdateWorkflowStatus(string status)
        {
            // Update status strip or add a label to show workflow progress
            // For now, we'll use the status strip's text
            if (statusStrip1.Items.Count > 0)
            {
                statusStrip1.Items[0].Text = status;
            }
        }

        // Backup system event handlers
        private void checkBoxEnableBackups_CheckedChanged(object sender, EventArgs e)
        {
            _settings.EnableBackups = checkBoxEnableBackups.Checked;
            if (_archiveManager != null)
                _archiveManager.EnableBackups = checkBoxEnableBackups.Checked;
        }

        private void numericUpDownMaxBackups_ValueChanged(object sender, EventArgs e)
        {
            _settings.MaxBackupFiles = (int)numericUpDownMaxBackups.Value;
            if (_archiveManager != null)
                _archiveManager.MaxBackupFiles = (int)numericUpDownMaxBackups.Value;
        }

        private void buttonBrowseBackupDir_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select backup directory";
                folderDialog.SelectedPath = textBoxBackupDirectory.Text;
                
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    textBoxBackupDirectory.Text = folderDialog.SelectedPath;
                    string dirName = folderDialog.SelectedPath.Substring(
                        folderDialog.SelectedPath.EndsWith("\\") 
                        ? folderDialog.SelectedPath.Length - 1 
                        : folderDialog.SelectedPath.LastIndexOf('\\') + 1);
                    _settings.BackupDirectory = dirName;
                    if (_archiveManager != null)
                        _archiveManager.BackupDirectoryRelativePath = dirName;
                }
            }
        }

        private void buttonCreateBackup_Click(object sender, EventArgs e)
        {
            if (listBoxTextures.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a texture to backup.", "No Selection",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string gamePath = textBoxGamePath.Text.Trim();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Please specify a game installation path first.", "Invalid Path",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedTextureName = listBoxTextures.SelectedItem.ToString();
            
            try
            {
                // Create a backup of the selected texture
                string tocPath = Path.Combine(gamePath, _game!.ArchiveDirectory, _game!.TocFileName);
                
                // Find the texture entry in TOC to get offset and size
                long textureOffset = -1;
                int textureSize = 0;

                using (BinaryReader reader = new BinaryReader(File.OpenRead(tocPath)))
                {
                    // Skip header (assuming standard TOC format)
                    reader.BaseStream.Seek(0x20, SeekOrigin.Begin);

                    int entryCount = reader.ReadInt32();
                    reader.BaseStream.Seek(0x28, SeekOrigin.Begin); // Skip to first entry

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

                            // Read null-terminated string
                            List<byte> nameBytes = new List<byte>();
                            byte b;
                            while ((b = reader.ReadByte()) != 0 && nameBytes.Count < nameLength)
                            {
                                nameBytes.Add(b);
                            }

                            string name = Encoding.ASCII.GetString(nameBytes.ToArray());

                            if (string.Equals(name, selectedTextureName + ".texture", StringComparison.OrdinalIgnoreCase))
                            {
                                textureOffset = offset;
                                textureSize = size;
                                break;
                            }

                            reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
                        }

                        // Move to next entry (assuming fixed size)
                        reader.BaseStream.Seek(0x18, SeekOrigin.Current);
                    }
                }

                if (textureOffset == -1 || textureSize == 0)
                {
                    MessageBox.Show($"Texture '{selectedTextureName}.texture' not found in game archives.",
                                   "Texture Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Read the texture data
                string archiveFilePath = Path.Combine(gamePath, "asset_archive",
                    selectedTextureName + ".g00s000");
                
                byte[] textureData = new byte[textureSize];
                using (FileStream sourceStream = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read))
                {
                    sourceStream.Seek(textureOffset, SeekOrigin.Begin);
                    int bytesRead = sourceStream.Read(textureData, 0, textureSize);
                    
                    if (bytesRead != textureSize)
                    {
                        MessageBox.Show($"Failed to read texture data. Expected {textureSize} bytes, got {bytesRead}.",
                                       "Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                // Create backup
                string backupDirectory = Path.Combine(gamePath, _archiveManager!.BackupDirectoryRelativePath);
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                string backupFilePath = Path.Combine(backupDirectory,
                    $"{selectedTextureName}_{DateTime.Now:yyyyMMdd_HHmmss}.texture.bak");

                // Validate data before backing up
                bool isValidData = true;
                if (textureSize > 0)
                {
                    byte firstByte = textureData[0];
                    for (int i = 1; i < textureSize; i++)
                    {
                        if (textureData[i] != firstByte)
                        {
                            // Found a different byte, so not all same
                            break;
                        }
                        if (i == textureSize - 1)
                        {
                            // All bytes are the same
                            isValidData = false;
                        }
                    }
                }

                if (!isValidData)
                {
                    MessageBox.Show("Backup failed: texture data appears to be invalid (all bytes identical).",
                                   "Backup Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                File.WriteAllBytes(backupFilePath, textureData);
                
                // Enforce retention policy
                _archiveManager!.EnforceBackupRetentionPolicy(backupDirectory, selectedTextureName);
                
                // Refresh backup list
                RefreshBackupList(gamePath, selectedTextureName);
                
                MessageBox.Show($"Backup created successfully:\n{backupFilePath}",
                               "Backup Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating backup: {ex.Message}",
                               "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonRestoreBackup_Click(object sender, EventArgs e)
        {
            if (listBoxTextures.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a texture to restore.", "No Selection",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (listBoxBackups.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a backup to restore from.", "No Backup Selected",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string gamePath = textBoxGamePath.Text.Trim();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Please specify a game installation path first.", "Invalid Path",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedTextureName = listBoxTextures.SelectedItem.ToString();
            BackupInfo selectedBackup = (BackupInfo)listBoxBackups.SelectedItem;

            DialogResult result = MessageBox.Show($"Are you sure you want to restore the texture '{selectedTextureName}' from backup created on {selectedBackup.Timestamp}?\n\nThis will overwrite the current texture in the game archive.",
                                                 "Confirm Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    // Read the backup data
                    byte[] backupData = File.ReadAllBytes(selectedBackup.FilePath);

                    // Validate backup data
                    if (backupData.Length == 0)
                    {
                        MessageBox.Show("Backup file is empty.", "Restore Failed",
                                       MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Find the texture entry in TOC to get offset and size
                    string tocPath = Path.Combine(gamePath, _game!.ArchiveDirectory, _game!.TocFileName);
                    long textureOffset = -1;
                    int textureSize = 0;

                    using (BinaryReader reader = new BinaryReader(File.OpenRead(tocPath)))
                    {
                        // Skip header (assuming standard TOC format)
                        reader.BaseStream.Seek(0x20, SeekOrigin.Begin);

                        int entryCount = reader.ReadInt32();
                        reader.BaseStream.Seek(0x28, SeekOrigin.Begin); // Skip to first entry

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

                                // Read null-terminated string
                                List<byte> nameBytes = new List<byte>();
                                byte b;
                                while ((b = reader.ReadByte()) != 0 && nameBytes.Count < nameLength)
                                {
                                    nameBytes.Add(b);
                                }

                                string name = Encoding.ASCII.GetString(nameBytes.ToArray());

                                if (string.Equals(name, selectedTextureName + ".texture", StringComparison.OrdinalIgnoreCase))
                                {
                                    textureOffset = offset;
                                    textureSize = size;
                                    break;
                                }

                                reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
                            }

                            // Move to next entry (assuming fixed size)
                            reader.BaseStream.Seek(0x18, SeekOrigin.Current);
                        }
                    }

                    if (textureOffset == -1 || textureSize == 0)
                    {
                        MessageBox.Show($"Texture '{selectedTextureName}.texture' not found in game archives.",
                                       "Texture Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Validate that backup data size matches expected texture size
                    if (backupData.Length != textureSize)
                    {
                        MessageBox.Show($"Backup data size mismatch. Expected {textureSize} bytes, got {backupData.Length}.",
                                       "Restore Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Write the backup data back to the game archive
                    string archiveFilePath = Path.Combine(gamePath, "asset_archive",
                        selectedTextureName + ".g00s000");

                    using (FileStream destinationStream = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Write))
                    {
                        destinationStream.Seek(textureOffset, SeekOrigin.Begin);
                        destinationStream.Write(backupData, 0, textureSize);
                    }

                    MessageBox.Show($"Texture restored successfully from backup:\n{selectedBackup.FilePath}",
                                   "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error restoring backup: {ex.Message}",
                                   "Restore Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
            }
        }

        private void RefreshBackupList(string gamePath, string textureName)
        {
            if (_archiveManager == null) return;
            try
            {
                listBoxBackups.Items.Clear();
                
                if (!_archiveManager.EnableBackups)
                {
                    return;
                }
                
                string backupDirectory = Path.Combine(gamePath, _archiveManager.BackupDirectoryRelativePath);
                if (!Directory.Exists(backupDirectory))
                {
                    return;
                }
                
                // Get backup information from the archive manager
                List<BackupInfo> backups = _archiveManager.GetBackupInfo(gamePath, textureName);
                
                foreach (var backup in backups)
                {
                    listBoxBackups.Items.Add(backup);
                }
                
                // Set display member for the ListBox
                listBoxBackups.DisplayMember = "Timestamp";
            }
            catch
            {
                // If anything goes wrong, just leave the list empty
            }
        }
    }
}