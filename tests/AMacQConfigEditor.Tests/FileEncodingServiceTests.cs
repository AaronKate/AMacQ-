using System.Text;
using AMacQConfigEditor.Services;
using Xunit;

namespace AMacQConfigEditor.Tests;

public sealed class FileEncodingServiceTests
{
    [Theory]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF }, "utf-8")]
    [InlineData(new byte[] { 0xFF, 0xFE }, "utf-16")]
    [InlineData(new byte[] { 0xFE, 0xFF }, "utf-16BE")]
    public void Detect_recognizes_known_boms(byte[] bytes, string expectedWebName)
    {
        using var input = new MemoryStream(bytes);

        Assert.Equal(expectedWebName, FileEncodingService.Detect(input).WebName);
    }

    [Fact]
    public void Detect_defaults_to_utf8_without_bom()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("value = 1"));

        var encoding = FileEncodingService.Detect(input);

        Assert.Equal("utf-8", encoding.WebName);
        Assert.Empty(encoding.GetPreamble());
    }
}
