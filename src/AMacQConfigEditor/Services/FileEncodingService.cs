using System.IO;
using System.Text;

namespace AMacQConfigEditor.Services;

public static class FileEncodingService
{
    public static Encoding Detect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> prefix = stackalloc byte[3];
        var originalPosition = stream.CanSeek ? stream.Position : 0;
        var count = stream.Read(prefix);
        if (stream.CanSeek)
        {
            stream.Position = originalPosition;
        }

        if (count >= 3 && prefix[..3].SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        if (count >= 2 && prefix[..2].SequenceEqual(new byte[] { 0xFF, 0xFE }))
        {
            return Encoding.Unicode;
        }

        if (count >= 2 && prefix[..2].SequenceEqual(new byte[] { 0xFE, 0xFF }))
        {
            return Encoding.BigEndianUnicode;
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    public static (string Content, Encoding Encoding) ReadAllText(string path)
    {
        using var stream = File.OpenRead(path);
        var encoding = Detect(stream);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: false);
        return (reader.ReadToEnd(), encoding);
    }
}
