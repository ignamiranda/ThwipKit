using System.Globalization;
using SpiderManModdingTool.Wpf.Converters;
using Xunit;

namespace SpiderManModdingTool.Wpf.Tests;

public sealed class ByteSizeConverterTests
{
    private readonly ByteSizeConverter _converter = new();

    [Theory]
    [InlineData(0U, "0 B")]
    [InlineData(1023U, "1,023 B")]
    [InlineData(1024U, "1.00 KB")]
    [InlineData(1048576U, "1.00 MB")]
    public void ConvertFormatsByteBoundaries(uint value, string expected)
    {
        Assert.Equal(expected, _converter.Convert(value, typeof(string), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertReturnsEmptyStringForInvalidInput()
    {
        Assert.Equal(string.Empty, _converter.Convert("1024", typeof(string), null!, CultureInfo.InvariantCulture));
    }
}
