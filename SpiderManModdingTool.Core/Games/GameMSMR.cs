namespace SpiderManModdingTool.Core.Games;

public sealed class GameMSMR : ConfiguredGame
{
    public GameMSMR() : base(GameDefinitionLoader.GetBuiltInDefinition("MSMR")) { }
}
