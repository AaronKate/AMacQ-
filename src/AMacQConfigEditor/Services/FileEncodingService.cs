using System.IO;
using System.Text;

namespace AMacQConfigEditor.Services;

/// <summary>
/// 配置文件编码识别。
///
/// 不能只看 BOM：中文 Windows 上用记事本"另存为 ANSI"得到的配置是 GBK，
/// 若一律按 UTF-8 读会变成乱码，保存时再以 UTF-8 写回就把中文注释永久改坏了。
/// 因此无 BOM 时先严格校验是否为合法 UTF-8，不合法则按系统 ANSI 代码页处理，
/// 并把识别结果一并返回，供写回时沿用同一编码。
/// </summary>
public static class FileEncodingService
{
    /// <summary>严格模式：遇到非法字节序列直接抛 DecoderFallbackException。</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static (string Content, Encoding Encoding) ReadAllText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var (encoding, offset) = DetectEncoding(bytes);
        return (encoding.GetString(bytes, offset, bytes.Length - offset), encoding);
    }

    /// <summary>返回识别出的编码与需要跳过的 BOM 长度。</summary>
    private static (Encoding Encoding, int Offset) DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return (Encoding.Unicode, 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return (Encoding.BigEndianUnicode, 2);

        return IsValidUtf8(bytes, out _)
            ? (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 0)
            : (Encoding.Default, 0); // 系统 ANSI 代码页（中文 Windows 即 GBK）
    }

    /// <summary>整份内容是否为合法 UTF-8；不合法时给出识别到的编码，便于提示用户。</summary>
    public static bool IsValidUtf8(byte[] bytes, out Encoding fallbackEncoding)
    {
        fallbackEncoding = Encoding.Default;
        try
        {
            StrictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
