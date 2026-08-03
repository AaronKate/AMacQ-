using System;
using System.IO;
using System.Text;

namespace AMacQConfigEditor.Services;

public static class AtomicFileWriter
{
    public static void WriteAllText(string path, string content, Encoding encoding)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        if (content is null) throw new ArgumentNullException(nameof(content));
        if (encoding is null) throw new ArgumentNullException(nameof(encoding));

        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, content, encoding);
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
