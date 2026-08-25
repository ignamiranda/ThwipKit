namespace ThwipKit.Core.Staging;

/// <summary>
/// A lightweight starting point for common modding workflows. Selecting a
/// template pre-fills new-project metadata so users do not start from a blank
/// slate. Templates never override project-specific asset tracking.
/// </summary>
public sealed class ProjectTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetGame { get; set; } = "MSMR";
    public string ModFormat { get; set; } = "spidermod";
    public string Author { get; set; } = string.Empty;
}

public static class ProjectTemplates
{
    public static IReadOnlyList<ProjectTemplate> All { get; } = new List<ProjectTemplate>
    {
        new ProjectTemplate
        {
            Name = "Blank",
            Description = "Start from an empty project.",
            TargetGame = "MSMR",
            ModFormat = "spidermod"
        },
        new ProjectTemplate
        {
            Name = "Texture Mod",
            Description = "Replace game textures with edited PNGs.",
            TargetGame = "MSMR",
            ModFormat = "spidermod"
        },
        new ProjectTemplate
        {
            Name = "Suit Mod",
            Description = "Add or reskin a playable suit.",
            TargetGame = "MSMR",
            ModFormat = "spidermod"
        },
        new ProjectTemplate
        {
            Name = "Stage Mod",
            Description = "Reorganize and rename staged assets.",
            TargetGame = "MSMR",
            ModFormat = "spidermod"
        }
    };

    public static ProjectTemplate? Find(string name)
        => All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
