using System.Text;
using ThwipKit.Core.Dat1;
using ThwipKit.Core.Games;

namespace ThwipKit.Core.Hashing;

/// <summary>
/// Derives a human-readable asset-name hash table from the user's own game install at runtime,
/// instead of shipping a pre-made community table. The Luna engine stores each asset reference as
/// (asset id, string offset) pairs inside DAT1 <c>ReferencesSection</c>s, where the string offset
/// points at a null-terminated path. This generator scans DAT1 buffers, resolves the paths, and
/// maps each asset id (in <see cref="HashComputer.ToAssetIdHex"/> form) to its path. The output
/// matches the format consumed by <see cref="GameBase.LoadHashTable"/> and <c>AssetCatalog</c>.
/// </summary>
public static class HashTableGenerator
{
    /// <summary>
    /// Builds the hash table from a set of DAT1 file buffers (already read from disk or decompressed
    /// from an archive). Buffers that are not valid DAT1 containers are skipped. Each reference whose
    /// path string resolves is added as <c>0x&lt;assetId&gt; = path</c>.
    /// </summary>
    /// <param name="dat1Files">Raw DAT1 container buffers.</param>
    /// <param name="requireHashMatch">
    /// When <see langword="true"/>, an entry is only added if <see cref="HashComputer.ComputeAssetId"/>
    /// of the resolved path equals the stored asset id (the same check ALERT performs). When
    /// <see langword="false"/> (default), every resolvable reference is added for maximum coverage.
    /// </param>
    public static Dictionary<string, string> BuildFromDat1Files(IEnumerable<byte[]> dat1Files, bool requireHashMatch = false)
    {
        ArgumentNullException.ThrowIfNull(dat1Files);

        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var buffer in dat1Files)
        {
            Dat1Container container;
            try
            {
                container = Dat1Container.Parse(buffer);
            }
            catch (InvalidDataException)
            {
                continue;
            }

            foreach (var (_, entries) in container.FindReferencesSections())
            {
                foreach (var entry in entries)
                {
                    string? path = container.GetStringAt(entry.StringOffset);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    if (requireHashMatch && HashComputer.ComputeAssetId(path!) != entry.AssetId)
                    {
                        continue;
                    }

                    string key = HashComputer.ToAssetIdHex(entry.AssetId);
                    if (!table.ContainsKey(key))
                    {
                        table[key] = path!;
                    }
                }
            }
        }

        return table;
    }

    /// <summary>
    /// Scans a directory tree for plain DAT1 files and builds the hash table from them. This is the
    /// integration entry point for runtime generation; callers that need to reach references stored
    /// inside compressed game archives should decompress those archives first and pass the resulting
    /// DAT1 buffers to <see cref="BuildFromDat1Files"/>.
    /// </summary>
    public static Dictionary<string, string> BuildFromDirectory(string rootDirectory, bool requireHashMatch = false)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {rootDirectory}");
        }

        var buffers = new List<byte[]>();
        foreach (string file in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(file);
            }
            catch (IOException)
            {
                continue;
            }

            if (bytes.Length >= 4 && BitConverter.ToUInt32(bytes, 0) == 0x44415431)
            {
                buffers.Add(bytes);
            }
        }

        return BuildFromDat1Files(buffers, requireHashMatch);
    }

    /// <summary>
    /// Builds the hash table by decompressing the game's DSAR archives and scanning every
    /// decompressed block for DAT1 containers. This is the production runtime-generation path:
    /// the asset path strings live inside DAT1 data files that the game stores compressed inside
    /// its <c>g00sXXX</c> archives, so the references can only be reached after decompression.
    /// </summary>
    public static Dictionary<string, string> BuildFromArchives(string archiveDirectory, GameBase game, bool requireHashMatch = false)
    {
        ArgumentNullException.ThrowIfNull(archiveDirectory);
        ArgumentNullException.ThrowIfNull(game);
        if (!Directory.Exists(archiveDirectory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {archiveDirectory}");
        }

        var manager = new ArchiveManager(game);
        var buffers = new List<byte[]>();
        foreach (string archive in Directory.EnumerateFiles(archiveDirectory))
        {
            string fileName = Path.GetFileName(archive);
            if (!fileName.StartsWith("g00s", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            IEnumerable<byte[]> blocks;
            try
            {
                blocks = manager.EnumerateDecompressedBlocks(archive);
            }
            catch (InvalidDataException)
            {
                continue;
            }
            catch (NotSupportedException)
            {
                continue;
            }

            foreach (var block in blocks)
            {
                buffers.AddRange(ExtractDat1Segments(block));
            }
        }

        return BuildFromDat1Files(buffers, requireHashMatch);
    }

    /// <summary>
    /// Scans a decompressed DSAR block (a run of concatenated asset data) for complete DAT1
    /// containers and returns each as an independent buffer.
    /// </summary>
    private static IEnumerable<byte[]> ExtractDat1Segments(byte[] block)
    {
        const uint Dat1Magic = 0x44415431;
        int cursor = 0;
        while (cursor + 16 <= block.Length)
        {
            if (BitConverter.ToUInt32(block, cursor) == Dat1Magic)
            {
                uint size = BitConverter.ToUInt32(block, cursor + 8);
                if (size >= 16 && cursor + size <= block.Length)
                {
                    byte[] dat1 = new byte[size];
                    Array.Copy(block, cursor, dat1, 0, (int)size);
                    yield return dat1;
                    cursor += (int)size;
                    continue;
                }
            }

            cursor++;
        }
    }

    /// <summary>
    /// Writes the hash table in <c>key=value</c> lines, sorted by key, so it can be loaded by
    /// <see cref="GameBase.LoadHashTable"/> from the game's <c>HashFilePath</c>.
    /// </summary>
    public static void WriteHashFile(Dictionary<string, string> table, string path)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(path);

        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        foreach (var pair in table.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteLine($"{pair.Key}={pair.Value}");
        }
    }
}
