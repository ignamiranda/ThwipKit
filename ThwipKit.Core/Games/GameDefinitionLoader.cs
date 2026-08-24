using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThwipKit.Core.GameDefinitions;

namespace ThwipKit.Core.Games;

public static class GameDefinitionLoader
{
    private static readonly Dictionary<string, GameDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void LoadDefinitions(string definitionsDirectory)
    {
        if (!Directory.Exists(definitionsDirectory))
        {
            throw new DirectoryNotFoundException($"Definitions directory not found: {definitionsDirectory}");
        }
        var loaded = new Dictionary<string, GameDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.GetFiles(definitionsDirectory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            GameDefinition definition = Deserialize(File.ReadAllText(file), file);
            AddDefinition(loaded, definition, file);
        }
        lock (Sync)
        {
            Definitions.Clear();
            foreach ((string id, GameDefinition definition) in loaded)
            {
                Definitions.Add(id, definition);
            }
        }
    }

    public static void LoadBuiltInDefinitions()
    {
        Assembly assembly = typeof(GameDefinitionLoader).Assembly;
        var loaded = new Dictionary<string, GameDefinition>(StringComparer.OrdinalIgnoreCase);
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".GameDefinitions.", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        foreach (string resource in resources)
        {
            using Stream stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidDataException($"Built-in game definition '{resource}' is unavailable.");
            using var reader = new StreamReader(stream);
            GameDefinition definition = Deserialize(reader.ReadToEnd(), resource);
            AddDefinition(loaded, definition, resource);
        }
        if (loaded.Count == 0)
        {
            throw new InvalidOperationException("No built-in game definitions were packaged.");
        }
        lock (Sync)
        {
            Definitions.Clear();
            foreach ((string id, GameDefinition definition) in loaded)
            {
                Definitions.Add(id, definition);
            }
        }
    }

    public static GameDefinition? GetDefinition(string internalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(internalId);
        lock (Sync)
        {
            Definitions.TryGetValue(internalId, out GameDefinition? definition);
            return definition;
        }
    }

    public static GameDefinition GetBuiltInDefinition(string internalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(internalId);
        bool needsLoad;
        lock (Sync)
        {
            needsLoad = Definitions.Count == 0;
        }
        if (needsLoad)
        {
            LoadBuiltInDefinitions();
        }
        lock (Sync)
        {
            return Definitions.TryGetValue(internalId, out GameDefinition? definition)
                ? definition
                : throw new ArgumentException($"Unknown built-in game ID: {internalId}", nameof(internalId));
        }
    }

    public static IReadOnlyDictionary<string, GameDefinition> GetAllDefinitions()
    {
        lock (Sync)
        {
            return new Dictionary<string, GameDefinition>(Definitions, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static GameDefinition Deserialize(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<GameDefinition>(json, JsonOptions)
                ?? throw new InvalidDataException($"Game definition '{source}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid game definition '{source}'.", exception);
        }
    }

    private static void AddDefinition(Dictionary<string, GameDefinition> definitions, GameDefinition definition, string source)
    {
        if (string.IsNullOrWhiteSpace(definition.InternalId))
        {
            throw new InvalidDataException($"Game definition '{source}' has no internal ID.");
        }
        if (!definitions.TryAdd(definition.InternalId, definition))
        {
            throw new InvalidDataException($"Duplicate game definition ID '{definition.InternalId}' in '{source}'.");
        }
    }
}
