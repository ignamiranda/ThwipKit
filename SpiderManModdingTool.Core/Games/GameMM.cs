namespace SpiderManModdingTool.Core.Games;

public sealed class GameMM : ConfiguredGame
{
    public GameMM() : base(GameDefinitionLoader.GetBuiltInDefinition("MM")) { }
}
