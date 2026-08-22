namespace SpiderManModdingTool.Core.Games;

public sealed class GameRCRA : ConfiguredGame
{
    public GameRCRA() : base(GameDefinitionLoader.GetBuiltInDefinition("RCRA")) { }
}
