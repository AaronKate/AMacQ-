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

    public static PackageDeploymentResult Deploy(string targetRoot, Action<PackageDeploymentProgress>? progress = null)
    {
        var root = Path.GetFullPath(targetRoot);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString()) ? root : root + Path.DirectorySeparatorChar;
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("未找到内置压缩包资源。");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);

        var entryPaths = archive.Entries
            .Select(entry => new ArchiveEntryPath(entry, SplitPath(entry.FullName)))
            .ToArray();
        var removeWrapperFolder = HasSingleWrapperFolder(entryPaths);
        var packageEntries = entryPaths
            .Select(item => new PackageEntry(item.Entry, GetRelativePath(item.PathParts, removeWrapperFolder)))
            .Where(item => item.RelativePath.Length > 0)
            .ToArray();
        var targets = packageEntries
            .GroupBy(item => item.RelativePath[0], StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var totalFiles = packageEntries.Count(item => item.Entry.Name.Length > 0);
        var completedFiles = 0;
        progress?.Invoke(new PackageDeploymentProgress(completedFiles, totalFiles, string.Empty));
        var extracted = new List<string>();
        var skipped = new List<string>();

        foreach (var target in targets)
        {
            var targetFiles = target.Where(item => item.Entry.Name.Length > 0).ToArray();
            var targetPath = GetSafePath(rootWithSeparator, new[] { target.Key });
            if (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                foreach (var _ in targetFiles)
                {
                    completedFiles++;
                    progress?.Invoke(new PackageDeploymentProgress(completedFiles, totalFiles, target.Key));
                }

                skipped.Add(target.Key);
                continue;
            }

            foreach (var item in target)
            {
                if (item.Entry.Name.Length == 0) continue;
                var destination = GetSafePath(rootWithSeparator, item.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                item.Entry.ExtractToFile(destination);
                completedFiles++;
                progress?.Invoke(new PackageDeploymentProgress(completedFiles, totalFiles, target.Key));
            }

            extracted.Add(target.Key);
        }

        return new PackageDeploymentResult(extracted, skipped);
    }

    private static bool HasSingleWrapperFolder(IReadOnlyList<ArchiveEntryPath> entryPaths)
    {
        var filePaths = entryPaths.Where(item => item.Entry.Name.Length > 0).Select(item => item.PathParts).ToArray();
        return filePaths.Length > 0
            && filePaths.All(path => path.Length > 1)
            && filePaths.Select(path => path[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
    }

    private static string[] GetRelativePath(string[] pathParts, bool removeWrapperFolder) =>
        removeWrapperFolder && pathParts.Length > 1 ? pathParts.Skip(1).ToArray() : pathParts;

    private static string[] SplitPath(string entryPath) =>
        entryPath.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

    private static string GetSafePath(string rootWithSeparator, IReadOnlyList<string> pathParts)
    {
        var pathSegments = new[] { rootWithSeparator }.Concat(pathParts).ToArray();
        var destination = Path.GetFullPath(Path.Combine(pathSegments));
        if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("压缩包中包含无效路径，已阻止解压。");
        }

        return destination;
    }

    private sealed record PackageEntry(ZipArchiveEntry Entry, string[] RelativePath);
    private sealed record ArchiveEntryPath(ZipArchiveEntry Entry, string[] PathParts);
}

internal sealed record PackageDeploymentProgress(int CompletedFiles, int TotalFiles, string CurrentTarget)
{
    public double Percentage => TotalFiles == 0 ? 100 : (double)CompletedFiles / TotalFiles * 100;
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
