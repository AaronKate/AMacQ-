using System.IO;
using System.Text;

namespace AMacQConfigEditor.Services;

public static class FileEncodingService
{
    public static Encoding Detect(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        var prefix = new byte[3];
        var originalPosition = stream.CanSeek ? stream.Position : 0;
        var count = stream.Read(prefix, 0, prefix.Length);
        if (stream.CanSeek)
        {
            stream.Position = originalPosition;
        }

        if (count >= 3 && prefix[0] == 0xEF && prefix[1] == 0xBB && prefix[2] == 0xBF)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        if (count >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (count >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    public static (string Content, Encoding Encoding) ReadAllText(string path)
    {
        using var stream = File.OpenRead(path);
        var encoding = Detect(stream);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
        return (reader.ReadToEnd(), encoding);
    }
}
