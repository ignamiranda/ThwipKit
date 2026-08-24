using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ThwipKit.Core.Mods;
using Xunit;

namespace ThwipKit.Core.Tests;

public class ModPackageTests : IDisposable
{
    private readonly string _tempDir;

    public ModPackageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Best effort
        }
    }

    private string CreateSourceDirectory(string name = "TestMod")
    {
        string source = Path.Combine(_tempDir, "source", name);
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "texture.texture"), [0x01, 0x02]);
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "nested", "config.config"), "{}");
        return source;
    }

    [Fact]
    public void CreateFromDirectory_ProducesValidPackageWithManifest()
    {
        string source = CreateSourceDirectory();
        string packagePath = Path.Combine(_tempDir, "TestMod.spidermod");

        // Create the manifest first
        File.WriteAllText(Path.Combine(source, "mod.json"),
            "{\"Name\":\"TestMod\",\"Author\":\"Tester\",\"TargetGame\":\"MSMR\"}");

        var package = ModPackage.CreateFromDirectory(source, packagePath);

        Assert.True(File.Exists(packagePath));
        Assert.Equal("TestMod", package.Manifest.Name);
        // Files list should include the two content files (manifest itself excluded)
        Assert.Equal(2, package.Manifest.Files.Count);
        Assert.Contains(package.Manifest.Files, f => f.RelativePath == "texture.texture");
        Assert.Contains(package.Manifest.Files, f => f.RelativePath == "nested/config.config");
    }

    [Fact]
    public void Open_WithoutManifest_Throws()
    {
        string badPackagePath = Path.Combine(_tempDir, "bad.spidermod");
        string fillerFile = Path.Combine(_tempDir, "filler.txt");
        File.WriteAllText(fillerFile, "content");

        using (FileStream fs = new(badPackagePath, FileMode.Create))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(fillerFile, "content.txt");
        }

        Assert.Throws<InvalidDataException>(() => ModPackage.Open(badPackagePath));
    }

    [Fact]
    public void OpenAndExtract_RoundTripsContent()
    {
        string source = CreateSourceDirectory();
        File.WriteAllText(Path.Combine(source, "mod.json"),
            "{\"Name\":\"RoundTrip\",\"Author\":\"T\",\"TargetGame\":\"MSMR\"}");
        string packagePath = Path.Combine(_tempDir, "roundtrip.spidermod");
        ModPackage.CreateFromDirectory(source, packagePath);

        ModPackage opened = ModPackage.Open(packagePath);
        string extractDir = Path.Combine(_tempDir, "extracted");
        opened.ExtractTo(extractDir);

        Assert.True(File.Exists(Path.Combine(extractDir, "mod.json")));
        Assert.Equal([0x01, 0x02], File.ReadAllBytes(Path.Combine(extractDir, "texture.texture")));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(extractDir, "nested", "config.config")));
    }

    [Fact]
    public void Extract_BlocksZipSlip()
    {
        string evilPackage = Path.Combine(_tempDir, "evil.spidermod");
        string victimFile = Path.Combine(_tempDir, "victim.txt");
        string manifestFile = Path.Combine(_tempDir, "evil-mod.json");
        File.WriteAllText(victimFile, "original");
        File.WriteAllText(manifestFile, "{\"Name\":\"Evil\",\"Author\":\"X\",\"TargetGame\":\"MSMR\",\"Files\":[]}");

        using (FileStream fs = new(evilPackage, FileMode.Create))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(manifestFile, "mod.json");
            archive.CreateEntryFromFile(victimFile, "..\\evil.txt");
        }

        ModPackage opened = ModPackage.Open(evilPackage);

        Assert.Throws<InvalidDataException>(() => opened.ExtractTo(Path.Combine(_tempDir, "dest")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "evil.txt")));
    }

    [Fact]
    public void ManifestValidation_RejectsDuplicatePaths()
    {
        var manifest = new ModManifest
        {
            Name = "Dup",
            Files =
            [
                new ModFileEntry { RelativePath = "a.texture" },
                new ModFileEntry { RelativePath = "A.TEXTURE" }
            ]
        };

        Assert.Throws<InvalidDataException>(manifest.Validate);
    }

    [Fact]
    public void ManifestValidation_RequiresNameAndFiles()
    {
        Assert.Throws<InvalidDataException>(() => new ModManifest { Name = "" }.Validate());
        Assert.Throws<InvalidDataException>(() => new ModManifest { Name = "NoFiles" }.Validate());
    }

    [Fact]
    public void SatisfiesDependencies_ChecksCaseInsensitively()
    {
        var manifest = new ModManifest
        {
            Name = "Dep",
            Files = [new ModFileEntry { RelativePath = "x" }],
            Dependencies = ["CoreLib"]
        };

        Assert.True(manifest.SatisfiesDependencies(["corelib"]));
        Assert.False(manifest.SatisfiesDependencies(["Other"]));
    }

    [Fact]
    public void Open_MissingPackage_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => ModPackage.Open(Path.Combine(_tempDir, "missing.spidermod")));
    }
}
