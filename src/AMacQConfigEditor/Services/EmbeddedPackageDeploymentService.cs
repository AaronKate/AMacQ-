using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace AMacQConfigEditor.Services;

internal static class EmbeddedPackageDeploymentService
{
    private const string ResourceName = "AMacQConfigEditor.Resources.SorinPackage.zip";

    public static PackageDeploymentResult Deploy(string targetRoot)
    {
        var root = Path.GetFullPath(targetRoot);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("未找到内置压缩包资源。");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);

        var packageEntries = archive.Entries
            .Select(entry => new PackageEntry(entry, GetRelativePath(entry.FullName)))
            .Where(item => item.RelativePath.Length > 0)
            .ToArray();
        var targets = packageEntries
            .GroupBy(item => item.RelativePath[0], StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var extracted = new List<string>();
        var skipped = new List<string>();

        foreach (var target in targets)
        {
            var targetPath = GetSafePath(rootWithSeparator, [target.Key]);
            if (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                skipped.Add(target.Key);
                continue;
            }

            foreach (var item in target)
            {
                if (item.Entry.Name.Length == 0) continue;
                var destination = GetSafePath(rootWithSeparator, item.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                item.Entry.ExtractToFile(destination);
            }

            extracted.Add(target.Key);
        }

        return new PackageDeploymentResult(extracted, skipped);
    }

    private static string[] GetRelativePath(string entryPath)
    {
        var parts = entryPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 1 ? [] : parts[1..];
    }

    private static string GetSafePath(string rootWithSeparator, IReadOnlyList<string> pathParts)
    {
        var destination = Path.GetFullPath(Path.Combine([rootWithSeparator, .. pathParts]));
        if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("压缩包中包含无效路径，已阻止解压。");
        }

        return destination;
    }

    private sealed record PackageEntry(ZipArchiveEntry Entry, string[] RelativePath);
}

internal sealed record PackageDeploymentResult(IReadOnlyList<string> ExtractedTargets, IReadOnlyList<string> SkippedTargets)
{
    public string ToDisplayMessage()
    {
        var lines = new List<string>();
        if (ExtractedTargets.Count > 0) lines.Add($"已解压：{string.Join("、", ExtractedTargets)}");
        if (SkippedTargets.Count > 0) lines.Add($"已跳过（目标已存在）：{string.Join("、", SkippedTargets)}");
        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "压缩包中没有可解压的文件。";
    }
}
