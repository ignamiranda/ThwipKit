using ThwipKit.Core.GameDefinitions;

namespace ThwipKit.Core.Games;

public static class GameFactory
{
    public static GameBase CreateGame(string gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        GameDefinition? definition = GameDefinitionLoader.GetDefinition(gameId);
        if (definition == null)
        {
            GameDefinitionLoader.LoadBuiltInDefinitions();
            definition = GameDefinitionLoader.GetBuiltInDefinition(gameId);
        }
        return CreateBuiltInWrapper(gameId, definition);
    }

    public static ConfiguredGame CreateGame(GameDefinition definition) => new(definition);

    public static GameBase CreateGameFromPath(string gamePath)
    {
        if (!Directory.Exists(gamePath))
        {
            throw new DirectoryNotFoundException($"Game directory not found: {gamePath}");
        }
        IReadOnlyDictionary<string, GameDefinition> definitions = EnsureDefinitions();
        var matches = definitions.Values
            .Where(definition => Executables(definition).Any(name => File.Exists(Path.Combine(gamePath, name))))
            .OrderBy(definition => definition.InternalId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return matches.Length switch
        {
            1 => CreateGame(matches[0]),
            0 => throw new InvalidOperationException($"No configured game profile matches '{gamePath}'."),
            _ => throw new InvalidOperationException($"Ambiguous game path '{gamePath}' matches: {string.Join(", ", matches.Select(match => match.InternalId))}.")
        };
    }

    public static GameBase CreateGameFromExecutable(string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Game executable not found.", executablePath);
        }
        string executable = Path.GetFileName(executablePath);
        GameDefinition[] matches = EnsureDefinitions().Values
            .Where(definition => Executables(definition).Contains(executable, StringComparer.OrdinalIgnoreCase))
            .OrderBy(definition => definition.InternalId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return matches.Length switch
        {
            1 => CreateGame(matches[0]),
            0 => throw new InvalidOperationException($"No configured game profile matches executable '{executable}'."),
            _ => throw new InvalidOperationException($"Ambiguous executable '{executable}' matches: {string.Join(", ", matches.Select(match => match.InternalId))}.")
        };
    }

    public static string DetectGameFromPath(string gamePath) => CreateGameFromPath(gamePath).InternalId;

    private static GameBase CreateBuiltInWrapper(string gameId, GameDefinition definition) => gameId.ToUpperInvariant() switch
    {
        "MSMR" => new GameMSMR(),
        "MM" => new GameMM(),
        "MSM2" => new GameMSM2(),
        "I30" => new GameI30(),
        "I33" => new GameI33(),
        "RCRA" => new GameRCRA(),
        _ => new ConfiguredGame(definition)
    };

    private static IReadOnlyDictionary<string, GameDefinition> EnsureDefinitions()
    {
        IReadOnlyDictionary<string, GameDefinition> definitions = GameDefinitionLoader.GetAllDefinitions();
        if (definitions.Count == 0)
        {
            GameDefinitionLoader.LoadBuiltInDefinitions();
            definitions = GameDefinitionLoader.GetAllDefinitions();
        }
        return definitions;
    }

    private static IEnumerable<string> Executables(GameDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.ExecutableName))
        {
            yield return definition.ExecutableName;
        }
        foreach (string executable in definition.SupportedExecutables)
        {
            if (!string.IsNullOrWhiteSpace(executable))
            {
                yield return executable;
            }
        }
    }
}
