namespace ThwipKit.Core.Staging;

public enum ProjectFormat
{
    Unknown = 0,
    ModProject = 1
}

public enum TrackedAssetStatus
{
    Extracted,
    Modified,
    Missing,
    Conflict,
    Deleted
}

public class ProjectMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetGame { get; set; } = "MSMR";
    public string ModFormat { get; set; } = "spidermod";
    public string GameVersion { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
}

public class TrackedAsset
{
    public ulong AssetId { get; set; }
    public string? ResolvedName { get; set; }
    public string ArchiveName { get; set; } = string.Empty;
    public uint Offset { get; set; }
    public uint Size { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public TrackedAssetStatus Status { get; set; } = TrackedAssetStatus.Extracted;
    public string? ReplacementSourcePath { get; set; }
    public long OriginalSizeBytes { get; set; }
    public DateTime? ExtractedUtc { get; set; }
    public DateTime? ModifiedUtc { get; set; }
    public string? ValidationHash { get; set; }
}

public class ProjectReference
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Source { get; set; }
    public bool Enabled { get; set; } = true;
}

public class ModProject
{
    public int SchemaVersion { get; set; } = 1;
    public ProjectFormat Format { get; set; } = ProjectFormat.ModProject;
    public ProjectMetadata Metadata { get; set; } = new();
    public List<TrackedAsset> Assets { get; set; } = new();
    public List<ProjectReference> References { get; set; } = new();
}