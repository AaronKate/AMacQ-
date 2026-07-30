using System.Text;

namespace AMacQConfigEditor.Models;

public sealed class ConfigFile(string path, string content, Encoding encoding)
{
    public string Path { get; } = path;
    public string Content { get; set; } = content;
    public Encoding Encoding { get; } = encoding;
}
