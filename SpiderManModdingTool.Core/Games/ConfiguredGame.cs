using SpiderManModdingTool.Core.GameDefinitions;

namespace SpiderManModdingTool.Core.Games;

public class ConfiguredGame : GameBase
{
    public ConfiguredGame(GameDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.InternalId))
        {
            throw new ArgumentException("Game definition must have an internal ID.", nameof(definition));
        }
    }

    public override GameDefinition Definition { get; }

    public override TocData ParseToc(string tocPath)
    {
        if (!string.Equals(Definition.TocFormat, "ZlibDat1", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"TOC format '{Definition.TocFormat}' is not supported.");
        }
        return TocParser.Parse(tocPath, Definition.SectionTags);
    }

    public override TocSectionData HandleSection(byte[] sectionTag, byte[] sectionData)
        => TocParser.ParseSectionData(sectionTag, sectionData, Definition.SectionTags);

    public override IReadOnlyDictionary<string, string> LoadHashTable(string gamePath)
    {
        if (!string.Equals(Definition.HashFormat, "KeyValue", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Hash format '{Definition.HashFormat}' is not supported.");
        }
        string path = Path.Combine(gamePath, Definition.HashFilePath);
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>();
        }
        return HashTableParser.Parse(File.ReadLines(path), path);
    }
}
