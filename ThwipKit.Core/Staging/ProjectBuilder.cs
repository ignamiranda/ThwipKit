using System;
using System.IO;
using System.Text.Json;
using ThwipKit.Core.Mods;

namespace ThwipKit.Core.Staging;

public sealed class ProjectBuilder
{
    private readonly ProjectManager _manager;
    private readonly StageManager _stageManager;

    public ProjectBuilder(ProjectManager manager, StageManager stageManager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _stageManager = stageManager ?? throw new ArgumentNullException(nameof(stageManager));
    }

    public string Build(string outputPath, string? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        string outputDir = Path.GetFullPath(outputPath);
        string filesDir = Path.Combine(outputDir, "files");
        Directory.CreateDirectory(filesDir);

        foreach (TrackedAsset asset in _manager.GetTrackedAssets())
        {
            string sourcePath = ((IProjectTracker)_manager).GetStageAbsolutePath(asset);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            string destinationPath = Path.Combine(filesDir, asset.RelativePath);
            string? destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        return outputDir;
    }

    public string TestBuild(string outputPath)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "thwip-test-" + Guid.NewGuid().ToString());
        return Build(Path.Combine(tempDir, outputPath));
    }

    public string Share(string outputPath, ModManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(manifest);

        string tempDir = Path.Combine(Path.GetTempPath(), "thwip-share-" + Guid.NewGuid().ToString());
        string buildDir = Build(Path.Combine(tempDir, "build"));

        File.WriteAllText(Path.Combine(buildDir, ModPackage.ManifestFileName), JsonSerializer.Serialize(manifest));
        ModPackage.CreateFromDirectory(buildDir, outputPath);
        return outputPath;
    }

    public void Package(string sourceDir, string outputPath, ModManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(sourceDir);
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(manifest);

        ModPackage.CreateFromDirectory(sourceDir, outputPath);
    }
}