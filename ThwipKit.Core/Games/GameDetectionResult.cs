namespace ThwipKit.Core.Games;

public abstract record GameDetectionResult
{
    public sealed record Match(GameBase Game) : GameDetectionResult;

    public sealed record NoMatch(string Path) : GameDetectionResult;

    public sealed record Ambiguous(string Path, IReadOnlyList<string> CandidateIds) : GameDetectionResult;
}
