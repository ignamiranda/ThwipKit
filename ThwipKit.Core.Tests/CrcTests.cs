using System.Text;
using ThwipKit.Core.Hashing;
using Xunit;

namespace ThwipKit.Core.Tests;

public class CrcTests
{
    [Fact]
    public void Crc32_KnownVectorIso()
    {
        byte[] data = Encoding.ASCII.GetBytes("123456789");
        Assert.Equal(0xCBF43926U, Crc32.Compute(data));
    }

    [Fact]
    public void Crc32_EmptyIsZero()
    {
        Assert.Equal(0U, Crc32.Compute(System.Array.Empty<byte>()));
    }

    [Fact]
    public void Crc32_IsDeterministicAndOrderDependent()
    {
        byte[] fox = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog");
        uint first = Crc32.Compute(fox);
        Assert.Equal(first, Crc32.Compute(fox));

        byte[] foxShifted = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy doh");
        Assert.NotEqual(first, Crc32.Compute(foxShifted));
    }

    [Fact]
    public void Crc64_KnownVectorXz()
    {
        byte[] data = Encoding.ASCII.GetBytes("123456789");
        Assert.Equal(0x995DC9BBDF1939FAUL, Crc64.Compute(data));
    }

    [Fact]
    public void Crc64_EmptyIsZero()
    {
        Assert.Equal(0UL, Crc64.Compute(System.Array.Empty<byte>()));
    }

    [Fact]
    public void Crc64_IsDeterministicAndOrderDependent()
    {
        byte[] fox = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog");
        ulong first = Crc64.Compute(fox);
        Assert.Equal(first, Crc64.Compute(fox));

        byte[] foxShifted = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy doh");
        Assert.NotEqual(first, Crc64.Compute(foxShifted));
    }
}
