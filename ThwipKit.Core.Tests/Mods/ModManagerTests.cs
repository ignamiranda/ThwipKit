using System;
using System.IO;
using System.Linq;
using ThwipKit.Core.Mods;
using Xunit;

namespace ThwipKit.Core.Tests;

public class ModManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ModManager _manager;

    public ModManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _manager = new ModManager(Path.Combine(_tempDir, "mods"));
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

    private string BuildPackage(string modName, string[]? dependencies = null)
    {
        string source = Path.Combine(_tempDir, "src", modName);
        Directory.CreateDirectory(source);

        string depsJson = dependencies is { Length: > 0 }
            ? $",\"Dependencies\":[{string.Join(",", dependencies.Select(d => $"\"{d}\""))}]"
            : "";

        File.WriteAllText(Path.Combine(source, "mod.json"),
            $"{{\"Name\":\"{modName}\",\"Author\":\"Tester\",\"Version\":\"1.0.0\",\"TargetGame\":\"MSMR\"{depsJson}}}");
        File.WriteAllBytes(Path.Combine(source, "content.texture"), [0xAB]);

        string packagePath = Path.Combine(_tempDir, $"{modName}.spidermod");
        ModPackage.CreateFromDirectory(source, packagePath);
        return packagePath;
    }

    [Fact]
    public void Install_RegistersModAndExtractsContent()
    {
        string package = BuildPackage("Alpha");

        InstalledMod installed = _manager.Install(package);

        Assert.True(installed.Enabled);
        Assert.True(_manager.IsInstalled("Alpha"));
        Assert.True(File.Exists(Path.Combine(_manager.GetModContentPath("Alpha"), "content.texture")));
    }

    [Fact]
    public void Install_Duplicate_Throws()
    {
        string package = BuildPackage("Dup");
        _manager.Install(package);

        Assert.Throws<InvalidOperationException>(() => _manager.Install(package));
    }

    [Fact]
    public void Install_MissingDependency_ThrowsAndExtractsNothing()
    {
        string package = BuildPackage("Needy", ["MissingLib"]);

        Assert.Throws<InvalidOperationException>(() => _manager.Install(package));
        Assert.False(_manager.IsInstalled("Needy"));
        Assert.False(Directory.Exists(Path.Combine(_manager.ModsDirectory, "Needy")));
    }

    [Fact]
    public void Uninstall_RemovesFilesAndRegistryEntry()
    {
        _manager.Install(BuildPackage("Gone"));
        _manager.Uninstall("Gone");

        Assert.False(_manager.IsInstalled("Gone"));
        Assert.False(Directory.Exists(Path.Combine(_manager.ModsDirectory, "Gone")));
    }

    [Fact]
    public void Uninstall_NotInstalled_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _manager.Uninstall("Ghost"));
    }

    [Fact]
    public void Uninstall_BlockedWhileDependentEnabled()
    {
        _manager.Install(BuildPackage("CoreLib"));
        _manager.Install(BuildPackage("Addon", ["CoreLib"]));

        Assert.Throws<InvalidOperationException>(() => _manager.Uninstall("CoreLib"));

        // After disabling the dependent, uninstall succeeds
        _manager.SetEnabled("Addon", false);
        _manager.Uninstall("CoreLib");
        Assert.False(_manager.IsInstalled("CoreLib"));
    }

    [Fact]
    public void SetEnabled_TogglesState()
    {
        _manager.Install(BuildPackage("Toggle"));

        _manager.SetEnabled("Toggle", false);
        Assert.False(_manager.GetInstalledMods().First(m => m.Name == "Toggle").Enabled);

        _manager.SetEnabled("Toggle", true);
        Assert.True(_manager.GetInstalledMods().First(m => m.Name == "Toggle").Enabled);
    }

    [Fact]
    public void SetEnabled_MissingDependencies_Throws()
    {
        _manager.Install(BuildPackage("Base"));
        _manager.Install(BuildPackage("Dependent", ["Base"]));

        // Disable the dependent first, then its dependency
        _manager.SetEnabled("Dependent", false);
        _manager.SetEnabled("Base", false);

        // Enabling while its dependency (Base) is disabled must fail
        Assert.Throws<InvalidOperationException>(() => _manager.SetEnabled("Dependent", true));
    }

    [Fact]
    public void Disable_BlockedWhileDependentEnabled()
    {
        _manager.Install(BuildPackage("Root"));
        _manager.Install(BuildPackage("Leaf", ["Root"]));

        Assert.Throws<InvalidOperationException>(() => _manager.SetEnabled("Root", false));

        _manager.SetEnabled("Leaf", false);
        _manager.SetEnabled("Root", false);
        Assert.False(_manager.GetInstalledMods().First(m => m.Name == "Root").Enabled);
    }

    [Fact]
    public void RegistryPersistsAcrossManagerInstances()
    {
        _manager.Install(BuildPackage("Persistent"));

        var freshManager = new ModManager(_manager.ModsDirectory);

        Assert.True(freshManager.IsInstalled("Persistent"));
        Assert.Single(freshManager.GetInstalledMods());
    }
}
