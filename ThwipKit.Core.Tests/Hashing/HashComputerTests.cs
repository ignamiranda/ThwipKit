using System.Text;
using ThwipKit.Core.Hashing;
using Xunit;

namespace ThwipKit.Core.Tests.Hashing;

public class HashComputerTests
{
    [Theory]
    [InlineData("characters/hero/models/hero.model", 0xBAC7C53F1F72A081UL)]
    [InlineData("textures/ui/loading.texture", 0x9EA3A7559DA1C267UL)]
    [InlineData("config/gameplay.config", 0x8D78877A43860247UL)]
    [InlineData("materials/metal.material", 0x98EB6640C2D140B6UL)]
    [InlineData("zones/nyc/zone00.zone", 0x9410612818926534UL)]
    [InlineData("models/props/barrel.model", 0x8E0BF5F014BFCD02UL)]
    public void ComputeAssetId_MatchesLunaEngine(string path, ulong expected)
    {
        Assert.Equal(expected, HashComputer.ComputeAssetId(path));
    }

    [Fact]
    public void Normalize_CollapsesSlashesAndLowercases()
    {
        // Verified against ALERT dat1lib.crc64.hash: both inputs produce the same id.
        const string mixed = "CHARACTERS\\HERO\\MODELS\\hero.model";
        Assert.Equal(HashComputer.ComputeAssetId("characters/hero/models/hero.model"), HashComputer.ComputeAssetId(mixed));
    }

    [Theory]
    [InlineData("characters/hero/models/hero.model", "0xBAC7C53F1F72A081")]
    [InlineData("models/props/barrel.model", "0x8E0BF5F014BFCD02")]
    public void ToAssetIdHex_MatchesAssetInfoFormat(string path, string expectedHex)
    {
        Assert.Equal(expectedHex, HashComputer.ToAssetIdHex(HashComputer.ComputeAssetId(path)));
    }
}
