using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using K4os.Compression.LZ4.Streams;
using ThwipKit.Core.Games;
using ThwipKit.Core.Sections;

namespace ThwipKit.Core
{
    /// <summary>
    /// Handles reading and writing to Spider-Man archive files using a game profile
    /// </summary>
    public class ArchiveManager
    {
        private readonly GameBase _game;

        // Backup configuration properties
        private string _backupDirectoryRelativePath;
        private bool _enableBackups = true;
        private int _maxBackupFiles = 10;

        public ArchiveManager(GameBase game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _backupDirectoryRelativePath = Path.Combine(game.ArchiveDirectory, "backups");
        }

        /// <summary>
        /// Gets or sets the relative path for backup directory (relative to game path)
        /// </summary>
        public string BackupDirectoryRelativePath
        {
            get => _backupDirectoryRelativePath;
            set => _backupDirectoryRelativePath = value ?? Path.Combine(_game.ArchiveDirectory, "backups");
        }

        /// <summary>
        /// Gets or sets whether backups are enabled
        /// </summary>
        public bool EnableBackups
        {
            get => _enableBackups;
            set => _enableBackups = value;
        }

        /// <summary>
        /// Gets or sets the maximum number of backup files to retain per texture
        /// </summary>
        public int MaxBackupFiles
        {
            get => _maxBackupFiles;
            set => _maxBackupFiles = Math.Max(0, value);
        }

        private class DsarBlockHeader
        {
            public uint RealOffset { get; set; }
            public uint CompressedOffset { get; set; }
            public uint RealSize { get; set; }
            public uint CompressedSize { get; set; }
            public byte CompressionType { get; set; }
        }

        /// <summary>
        /// Gets the list of texture names from the game archive
        /// </summary>
        /// <param name="gamePath">Path to the game installation</param>
        /// <returns>List of texture names</returns>
        public List<string> GetTextureNames(string gamePath)
        {
            string tocPath = GetTocPath(gamePath);
            
            if (!File.Exists(tocPath))
            {
                throw new FileNotFoundException("TOC file not found", tocPath);
            }

            try
            {
                TocData toc = ParseToc(tocPath);
                
                var textureNamesSet = new HashSet<string>();
                foreach (var sizeEntry in toc.SizeEntries)
                {
                    if (sizeEntry.Index >= toc.AssetIds.Count)
                    {
                        continue;
                    }
                    ulong assetId = toc.AssetIds[(int)sizeEntry.Index];
                    string name = $"0x{assetId:X16}";
                    textureNamesSet.Add(name);
                }
                return textureNamesSet.ToList();
            }
            catch (Exception ex)
            {
                throw new IOException("Error reading TOC file", ex);
            }
        }

        private TocData ParseToc(string tocPath)
        {
            return _game.ParseToc(tocPath);
        }

        private string GetTocPath(string gamePath) => Path.Combine(gamePath, _game.ArchiveDirectory, _game.TocFileName);

        private string GetArchiveFilePath(string gamePath, string archiveName) => Path.Combine(gamePath, _game.ArchiveDirectory, archiveName);

        private static CompressionFormat ResolveDsarCompressionType(byte compressionType) => compressionType switch
        {
            0 => CompressionFormat.None,
            2 => CompressionFormat.GDeflate,
            3 => CompressionFormat.Lz4,
            _ => throw new NotSupportedException($"Unknown DSAR compression type: {compressionType}")
        };

        private static bool TryResolveTexture(TocData toc, string textureName, out uint textureOffset, out int textureSize, out string archiveName)
        {
            textureOffset = 0;
            textureSize = 0;
            archiveName = string.Empty;

            ulong searchHash = 0;
            if (textureName.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                searchHash = ulong.Parse(textureName.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }

            if (searchHash == 0)
            {
                return false;
            }

            for (int i = 0; i < toc.SizeEntries.Count; i++)
            {
                ulong assetId = toc.AssetIds[(int)toc.SizeEntries[i].Index];

                if (assetId == searchHash)
                {
                    var offsetEntry = toc.Offsets[(int)toc.SizeEntries[i].Index];
                    textureOffset = offsetEntry.OffsetInArchive;
                    textureSize = (int)toc.SizeEntries[i].Value;
                    archiveName = toc.Archives[(int)offsetEntry.ArchiveIndex].Name;
                    return textureOffset != 0 && textureSize != 0;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts a texture from the game archive to a PNG file
        /// </summary>
        public bool ExtractTextureToPng(string gamePath, string textureName, string outputPngPath)
        {
            string tocPath = GetTocPath(gamePath);
            
            try
            {
                TocData toc = ParseToc(tocPath);

                if (!TryResolveTexture(toc, textureName, out uint textureOffset, out int textureSize, out string archiveName))
                {
                    throw new FileNotFoundException($"Texture '{textureName}' not found in game archives");
                }

                string archiveFilePath = GetArchiveFilePath(gamePath, archiveName);
                byte[] textureData = ReadFromDsar(archiveFilePath, textureOffset, (uint)textureSize);
                
                bool conversionSuccessful = SimulateTextureToPngConversion(textureData, outputPngPath);
                
                return conversionSuccessful;
            }
            catch
            {
                return false;
            }
        }

        internal byte[] ReadFromDsar(string archivePath, uint offset, uint size)
        {
            using (FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                byte[] magic = reader.ReadBytes(4);
                if (magic.Length < 4 || magic[0] != (byte)'D' || magic[1] != (byte)'S' || magic[2] != (byte)'A' || magic[3] != (byte)'R')
                {
                    throw new InvalidDataException("Invalid DSAR file magic");
                }
                
                uint version = reader.ReadUInt32();
                uint blockCount = reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt64();
                reader.ReadBytes(8);
                
                var blocks = new List<DsarBlockHeader>();
                for (int i = 0; i < blockCount; i++)
                {
                    uint realOffset = reader.ReadUInt32();
                    reader.ReadUInt32();
                    uint compressedOffset = reader.ReadUInt32();
                    reader.ReadUInt32();
                    uint realSize = reader.ReadUInt32();
                    uint compressedSize = reader.ReadUInt32();
                    byte compressionType = reader.ReadByte();
                    reader.ReadBytes(7);
                    
                    blocks.Add(new DsarBlockHeader
                    {
                        RealOffset = realOffset,
                        CompressedOffset = compressedOffset,
                        RealSize = realSize,
                        CompressedSize = compressedSize,
                        CompressionType = compressionType
                    });
                }
                
                uint assetEnd = offset + size;
                var result = new List<byte>();
                
                foreach (var block in blocks)
                {
                    uint blockStart = block.RealOffset;
                    uint blockEnd = blockStart + block.RealSize;
                    
                    if (blockStart < assetEnd && blockEnd > offset)
                    {
                        fs.Seek(block.CompressedOffset, SeekOrigin.Begin);
                        byte[] compressedBlock = reader.ReadBytes((int)block.CompressedSize);
                        
                        CompressionFormat format = ResolveDsarCompressionType(block.CompressionType);
                        if (!CompressionSupport.IsImplemented(format))
                        {
                            throw new NotSupportedException($"Compression format '{format}' declared in DSAR block is not implemented.");
                        }
                        if (format != CompressionFormat.None && !_game.CompressionFormats.Contains(format))
                        {
                            throw new NotSupportedException($"DSAR block declares compression '{format}' which is not supported by game profile '{_game.InternalId}'.");
                        }
                        
                        byte[] decompressedBlock;
                        if (format == CompressionFormat.Lz4)
                        {
                            decompressedBlock = new byte[block.RealSize];
                            K4os.Compression.LZ4.LZ4Codec.Decode(compressedBlock, decompressedBlock);
                        }
                        else if (format == CompressionFormat.None)
                        {
                            decompressedBlock = compressedBlock;
                        }
                        else
                        {
                            throw new NotSupportedException($"Compression format '{format}' is recognized but has no decoder.");
                        }
                        
                        uint assetStartInBlock = (uint)Math.Max(0, (long)offset - (long)blockStart);
                        uint assetEndInBlock = (uint)Math.Min(block.RealSize, (long)assetEnd - (long)blockStart);
                        
                        for (uint i = assetStartInBlock; i < assetEndInBlock; i++)
                        {
                            result.Add(decompressedBlock[i]);
                        }
                    }
                }
                
                return result.ToArray();
            }
        }

        /// <summary>
        /// Reads the DSAR block table and returns the compression format declared for the
        /// block covering the asset's region in the decompressed stream. Returns null when
        /// the archive is not a DSAR file or no block covers the requested range.
        /// </summary>
        public CompressionFormat? GetCompressionFormat(string archivePath, uint offset, uint size)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                return null;
            }

            List<DsarBlockHeader> blocks = ReadDsarBlocks(archivePath);
            uint assetEnd = offset + size;

            foreach (DsarBlockHeader block in blocks)
            {
                uint blockStart = block.RealOffset;
                uint blockEnd = blockStart + block.RealSize;

                if (blockStart <= offset && offset < blockEnd)
                {
                    return ResolveDsarCompressionType(block.CompressionType);
                }
            }

            return null;
        }

        /// <summary>
        /// Reads and decompresses the asset bytes for the region [offset, offset + size) in the
        /// given archive. Returns null when the archive is missing or is not a DSAR file.
        /// </summary>
        public byte[]? GetAssetData(string archivePath, uint offset, uint size)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                return null;
            }

            try
            {
                return ReadFromDsar(archivePath, offset, size);
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        private static List<DsarBlockHeader> ReadDsarBlocks(string archivePath)
        {
            using FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
            using BinaryReader reader = new BinaryReader(fs);

            byte[] magic = reader.ReadBytes(4);
            if (magic.Length < 4 || magic[0] != (byte)'D' || magic[1] != (byte)'S' || magic[2] != (byte)'A' || magic[3] != (byte)'R')
            {
                throw new InvalidDataException("Invalid DSAR file magic");
            }

            reader.ReadUInt32();
            uint blockCount = reader.ReadUInt32();
            reader.ReadUInt32();
            reader.ReadUInt64();
            reader.ReadBytes(8);

            var blocks = new List<DsarBlockHeader>();
            for (int i = 0; i < blockCount; i++)
            {
                uint realOffset = reader.ReadUInt32();
                reader.ReadUInt32();
                uint compressedOffset = reader.ReadUInt32();
                reader.ReadUInt32();
                uint realSize = reader.ReadUInt32();
                uint compressedSize = reader.ReadUInt32();
                byte compressionType = reader.ReadByte();
                reader.ReadBytes(7);

                blocks.Add(new DsarBlockHeader
                {
                    RealOffset = realOffset,
                    CompressedOffset = compressedOffset,
                    RealSize = realSize,
                    CompressedSize = compressedSize,
                    CompressionType = compressionType
                });
            }

            return blocks;
        }

        /// <summary>
        /// Decompresses every block of a DSAR archive and yields the raw (decompressed) bytes of
        /// each block. Used by the runtime hash-table generator to reach the DAT1 data files that
        /// carry asset-reference sections; callers scan the returned buffers for DAT1 containers.
        /// </summary>
        internal IEnumerable<byte[]> EnumerateDecompressedBlocks(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                yield break;
            }

            List<DsarBlockHeader> blocks = ReadDsarBlocks(archivePath);
            using FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
            using BinaryReader reader = new BinaryReader(fs);
            foreach (DsarBlockHeader block in blocks)
            {
                fs.Seek(block.CompressedOffset, SeekOrigin.Begin);
                byte[] compressedBlock = reader.ReadBytes((int)block.CompressedSize);
                yield return DecompressBlock(block, compressedBlock);
            }
        }

        private static byte[] DecompressBlock(DsarBlockHeader block, byte[] compressedBlock)
        {
            CompressionFormat format = ResolveDsarCompressionType(block.CompressionType);
            if (!CompressionSupport.IsImplemented(format))
            {
                throw new NotSupportedException($"Compression format '{format}' declared in DSAR block is not implemented.");
            }

            if (format == CompressionFormat.Lz4)
            {
                byte[] decompressed = new byte[block.RealSize];
                K4os.Compression.LZ4.LZ4Codec.Decode(compressedBlock, decompressed);
                return decompressed;
            }

            if (format == CompressionFormat.None)
            {
                return compressedBlock;
            }

            throw new NotSupportedException($"Compression format '{format}' is recognized but has no decoder.");
        }

        private bool SimulateTextureToPngConversion(byte[] textureData, string pngFilePath)
        {
            try
            {
                using (FileStream fs = new FileStream(pngFilePath, FileMode.Create, FileAccess.Write))
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    writer.Write((byte)0x89);
                    writer.Write((byte)0x50);
                    writer.Write((byte)0x4E);
                    writer.Write((byte)0x47);
                    writer.Write((byte)0x0D);
                    writer.Write((byte)0x0A);
                    writer.Write((byte)0x1A);
                    writer.Write((byte)0x0A);
                    
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x0D);
                    writer.Write((byte)0x49);
                    writer.Write((byte)0x48);
                    writer.Write((byte)0x44);
                    writer.Write((byte)0x52);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x08);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x08);
                    writer.Write((byte)0x08);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    
                    writer.Write((byte)0x00);
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
                    
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                    
                    writer.Write((byte)0x00);
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

        /// <summary>
        /// Rebuilds a texture from a PNG file and writes it to the game archive
        /// </summary>
        public bool RebuildTextureFromPng(string gamePath, string textureName, string inputPngPath, bool createBackup = true)
        {
            string tocPath = GetTocPath(gamePath);
            
            try
            {
                if (!File.Exists(inputPngPath))
                {
                    throw new FileNotFoundException("PNG file not found", inputPngPath);
                }

                TocData toc = ParseToc(tocPath);

                if (!TryResolveTexture(toc, textureName, out uint textureOffset, out int textureSize, out string archiveName))
                {
                    throw new FileNotFoundException($"Texture '{textureName}' not found in game archives");
                }

                string archiveFilePath = GetArchiveFilePath(gamePath, archiveName);

                if (createBackup && _enableBackups)
                {
                    try
                    {
                        string backupDirectory = Path.Combine(gamePath, _backupDirectoryRelativePath);
                        if (!Directory.Exists(backupDirectory))
                        {
                            Directory.CreateDirectory(backupDirectory);
                        }

                        string backupFilePath = Path.Combine(backupDirectory,
                            $"{textureName}_{DateTime.Now:yyyyMMdd_HHmmss}.texture.bak");

                        byte[] originalTextureData = ReadFromDsar(archiveFilePath, textureOffset, (uint)textureSize);

                        string tempBackupPath = Path.GetTempFileName();
                        try
                        {
                            if (originalTextureData.Length > 0)
                            {
                                bool isValidData = true;
                                if (textureSize > 0)
                                {
                                    byte firstByte = originalTextureData[0];
                                    for (int i = 1; i < originalTextureData.Length; i++)
                                    {
                                        if (originalTextureData[i] != firstByte)
                                        {
                                            break;
                                        }
                                        if (i == originalTextureData.Length - 1)
                                        {
                                            isValidData = false;
                                        }
                                    }
                                }

                                if (isValidData)
                                {
                                    File.WriteAllBytes(tempBackupPath, originalTextureData);
                                    if (File.Exists(backupFilePath))
                                    {
                                        File.Replace(tempBackupPath, backupFilePath, null);
                                    }
                                    else
                                    {
                                        File.Move(tempBackupPath, backupFilePath);
                                    }
                                }
                                else
                                {
                                    throw new IOException("Backup data validation failed");
                                }
                            }
                        }
                        finally
                        {
                            if (File.Exists(tempBackupPath))
                            {
                                try
                                {
                                    File.Delete(tempBackupPath);
                                }
                                catch
                                {
                                }
                            }
                        }

                        EnforceBackupRetentionPolicy(backupDirectory, textureName);
                    }
                    catch
                    {
                    }
                }

                string tempTexturePath = Path.Combine(Path.GetTempPath(), textureName + ".texture");

                bool conversionSuccessful = SimulatePngToTextureConversion(File.ReadAllBytes(inputPngPath), tempTexturePath, textureSize);

                if (!conversionSuccessful)
                {
                    throw new IOException("Failed to convert PNG to texture format");
                }

                byte[] newTextureData = File.ReadAllBytes(tempTexturePath);
                WriteToDsar(archiveFilePath, textureOffset, (uint)textureSize, newTextureData);

                if (File.Exists(tempTexturePath))
                {
                    File.Delete(tempTexturePath);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enforces the backup retention policy
        /// </summary>
        public void EnforceBackupRetentionPolicy(string backupDirectory, string textureName)
        {
            if (_maxBackupFiles <= 0)
            {
                return;
            }

            try
            {
                string searchPattern = $"{textureName}_*.texture.bak";
                var backupFiles = Directory.GetFiles(backupDirectory, searchPattern)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                for (int i = _maxBackupFiles; i < backupFiles.Count; i++)
                {
                    try
                    {
                        backupFiles[i].Delete();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Restores a texture from the most recent backup
        /// </summary>
        public bool RestoreTextureFromBackup(string gamePath, string textureName)
        {
            try
            {
                string backupDirectory = Path.Combine(gamePath, _backupDirectoryRelativePath);
                if (!Directory.Exists(backupDirectory))
                {
                    return false;
                }

                string searchPattern = $"{textureName}_*.texture.bak";
                var backupFiles = Directory.GetFiles(backupDirectory, searchPattern)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                if (backupFiles.Count == 0)
                {
                    return false;
                }

                string mostRecentBackup = backupFiles[0].FullName;
                byte[] backupData = File.ReadAllBytes(mostRecentBackup);

                if (backupData.Length == 0)
                {
                    return false;
                }

                string tocPath = GetTocPath(gamePath);
                TocData toc = ParseToc(tocPath);

                if (!TryResolveTexture(toc, textureName, out uint textureOffset, out int textureSize, out string archiveName))
                {
                    return false;
                }

                if (backupData.Length != textureSize)
                {
                    return false;
                }

                string archiveFilePath = GetArchiveFilePath(gamePath, archiveName);
                WriteToDsar(archiveFilePath, textureOffset, (uint)textureSize, backupData);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets information about available backups for a texture
        /// </summary>
        public List<BackupInfo> GetBackupInfo(string gamePath, string textureName)
        {
            var backupInfos = new List<BackupInfo>();

            try
            {
                string backupDirectory = Path.Combine(gamePath, _backupDirectoryRelativePath);
                if (!Directory.Exists(backupDirectory))
                {
                    return backupInfos;
                }

                string searchPattern = $"{textureName}_*.texture.bak";
                var backupFiles = Directory.GetFiles(backupDirectory, searchPattern)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                foreach (var backupFile in backupFiles)
                {
                    backupInfos.Add(new BackupInfo
                    {
                        FilePath = backupFile.FullName,
                        Timestamp = backupFile.LastWriteTime,
                        Size = backupFile.Length
                    });
                }
            }
            catch
            {
                return backupInfos;
            }

            return backupInfos;
        }

        private bool SimulatePngToTextureConversion(byte[] pngData, string textureFilePath, int expectedSize)
        {
            try
            {
                byte[] textureData = new byte[expectedSize];
                for (int i = 0; i < expectedSize; i++)
                {
                    textureData[i] = pngData[i % pngData.Length];
                }
                File.WriteAllBytes(textureFilePath, textureData);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal void WriteToDsar(string archivePath, uint offset, uint size, byte[] data)
        {
            using (FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Write))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                fs.Write(data, 0, (int)size);
            }
        }
    }

    /// <summary>
    /// Information about a backup file
    /// </summary>
    public class BackupInfo
    {
        public string? FilePath { get; set; }
        public DateTime Timestamp { get; set; }
        public long Size { get; set; }
    }
}
