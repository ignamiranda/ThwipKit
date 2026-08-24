using System.Text;
using ThwipKit.Core.Hashing;
using Xunit;

namespace ThwipKit.Core.Tests;

public class CrcTests
{
    [Fact]
    public void Crc32MatchesKnownVector()
    {
        byte[] data = Encoding.ASCII.GetBytes("123456789");

        uint crc = Crc32.Compute(data);

        Assert.Equal(0xCBF43926u, crc);
        Assert.Equal("0xCBF43926", Crc32.ComputeHex(data));
    }

    [Fact]
    public void Crc64MatchesKnownVector()
    {
        byte[] data = Encoding.ASCII.GetBytes("123456789");

        ulong crc = Crc64.Compute(data);

        Assert.Equal(0x995DC9BBDF1939FAuL, crc);
        Assert.Equal("0x995DC9BBDF1939FA", Crc64.ComputeHex(data));
    }

    [Fact]
    public void Crc32IsStableAcrossCalls()
    {
        byte[] data = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog");

        Assert.Equal(Crc32.Compute(data), Crc32.Compute(data));
    }
}
