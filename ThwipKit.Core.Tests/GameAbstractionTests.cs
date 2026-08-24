using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using ThwipKit.Core.Sections;
using Xunit;

namespace ThwipKit.Core.Tests;

public class GameAbstractionTests
{
    [Fact]
    public void ParseTocReturnsParsedSections()
    {
        string tocPath = TestFileFixtures.CreateTocFile();

        try
        {
            var result = Assert.IsType<TocData>(new GameMSMR().ParseToc(tocPath));

            Assert.Equal("Archive0", result.Archives.Single().Name);
            Assert.Equal(0x1122334455667788UL, result.AssetIds.Single());
            Assert.Equal(123U, result.SizeEntries.Single().Value);
            Assert.Equal(0U, result.SizeEntries.Single().Index);
            Assert.Equal(0U, result.Offsets.Single().ArchiveIndex);
            Assert.Equal(456U, result.Offsets.Single().OffsetInArchive);
        }
        finally
        {
            File.Delete(tocPath);
        }
    }

    [Fact]
    public void PublicDat1ParserParsesUnwrappedPayload()
    {
        (string path, byte[] dat1) = TestFileFixtures.CreateTocFixture();
        try
        {
            TocData result = TocParser.ParseDat1(dat1, new GameMSMR().Definition.SectionTags);

            Assert.Equal("Archive0", result.Archives.Single().Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseTocRejectsInvalidMagic()
    {
        string tocPath = Path.GetTempFileName();
        File.WriteAllBytes(tocPath, [0, 1, 2, 3]);

        try
        {
            Assert.Throws<InvalidDataException>(() => new GameMSMR().ParseToc(tocPath));
        }
        finally
        {
            File.Delete(tocPath);
        }
    }

    [Fact]
    public void ArchivesMapParserReadsEveryEntry()
    {
        byte[] data = TestFileFixtures.Combine(TestFileFixtures.CreateArchiveEntry("Archive0"), TestFileFixtures.CreateArchiveEntry("Archive1"));

        List<ArchivesMapSection> entries = ArchivesMapSection.Parse(data);

        Assert.Equal(["Archive0", "Archive1"], entries.Select(entry => entry.Name));
    }

    [Fact]
    public void SizeEntryParserUsesDocumentedFieldOrder()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(1U);
            writer.Write(123U);
            writer.Write(7U);
        }

        SizeEntriesSection entry = Assert.Single(SizeEntriesSection.Parse(stream.ToArray()));

        Assert.Equal(123U, entry.Value);
        Assert.Equal(7U, entry.Index);
    }

    [Fact]
    public void SectionParsersRejectPartialRecords()
    {
        Assert.Throws<InvalidDataException>(() => AssetIdsSection.Parse(new byte[7]));
        Assert.Throws<InvalidDataException>(() => SizeEntriesSection.Parse(new byte[11]));
        Assert.Throws<InvalidDataException>(() => OffsetsSection.Parse(new byte[7]));
        Assert.Throws<InvalidDataException>(() => ArchivesMapSection.Parse(new byte[71]));
    }

    [Fact]
    public void ConfiguredGameSupportsNewProfileWithoutFactorySwitch()
    {
        var definition = new GameDefinition
        {
            DisplayName = "Custom Profile",
            InternalId = "CUSTOM",
            TocFormat = TocFormat.ZlibDat1,
            CompressionFormats = [CompressionFormat.Zlib],
            SectionTags = new Dictionary<string, string>
            {
                ["ArchivesMap"] = "F0BF8A39",
                ["AssetIDs"] = "8A7B6D50",
                ["SizeEntries"] = "61F4BC65",
                ["Offsets"] = "B520D7DC"
            }
        };

        ConfiguredGame game = GameFactory.CreateGame(definition);

        Assert.Equal("CUSTOM", game.InternalId);
        Assert.Equal(CompressionFormat.Zlib, game.DetectCompression("unused"));
    }

    [Fact]
    public void FactoryCreatesExternallyLoadedProfileByIdWithoutSwitch()
    {
        string path = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(path, "custom.json"), """
                {
                  "DisplayName": "Custom Profile",
                  "InternalId": "CUSTOM",
                  "TocFormat": "ZlibDat1",
                  "CompressionFormats": ["Zlib"]
                }
                """);
            GameDefinitionLoader.LoadDefinitions(path);

            Assert.Equal("CUSTOM", GameFactory.CreateGame("CUSTOM").InternalId);
        }
        finally
        {
            Directory.Delete(path, true);
            GameDefinitionLoader.LoadBuiltInDefinitions();
        }
    }

    [Fact]
    public void FactoryRejectsNoMatchAndAmbiguity()
    {
        string path = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.Throws<InvalidOperationException>(() => GameFactory.CreateGameFromPath(path));
            File.WriteAllBytes(Path.Combine(path, "Spider-Man Remastered.exe"), []);
            Assert.Equal("MSMR", GameFactory.CreateGameFromPath(path).InternalId);
            File.WriteAllBytes(Path.Combine(path, "SpiderManMM.exe"), []);
            Assert.Throws<InvalidOperationException>(() => GameFactory.CreateGameFromPath(path));
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public void HashParserHandlesCommentsFirstSeparatorAndDuplicateDiagnostics()
    {
        IReadOnlyDictionary<string, string> result = HashTableParser.Parse(["", "# comment", "; comment", "// comment", "key=value=tail"]);

        Assert.Equal("value=tail", result["key"]);
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => HashTableParser.Parse(["key=one", "KEY=two"]));
        Assert.Contains("line 2", exception.Message);
    }

    [Fact]
    public void VersionDetectionUsesProfileVersionFileAndLabelsStandalone()
    {
        string path = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(path, "build.version"), "2.3.4");
            var definition = new GameDefinition { InternalId = "CUSTOM", VersionFileNames = ["build.version"] };

            GameVersionInfo result = new GameVersionDetector().DetectVersion(path, definition);

            Assert.Equal("2.3.4", result.VersionString);
            Assert.Equal("Standalone", result.DistributionPlatform);
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public void VersionDetectionRecognizesEgstoreDirectory()
    {
        string path = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(path, ".egstore"));
            File.WriteAllText(Path.Combine(path, "version.txt"), "2.3.4");
            var definition = new GameDefinition { InternalId = "CUSTOM", VersionFileNames = ["version.txt"] };

            GameVersionInfo result = new GameVersionDetector().DetectVersion(path, definition);

            Assert.Equal("Epic Games", result.DistributionPlatform);
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public void BuiltInDefinitionsAreEmbeddedAndCreatableById()
    {
        GameDefinitionLoader.LoadBuiltInDefinitions();

        Assert.Equal("MSMR", GameFactory.CreateGame("MSMR").InternalId);
        Assert.Equal(6, GameDefinitionLoader.GetAllDefinitions().Count);
    }
}
