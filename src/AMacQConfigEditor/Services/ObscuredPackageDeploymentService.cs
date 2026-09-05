using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AMacQConfigEditor.Services;

internal static class ObscuredPackageDeploymentService
{
    private const string ResourceName = "AMacQConfigEditor.Resources.SorinPackage.zip";
    private const string LauncherName = "GHUB - Sorin 25.1 S11-1.lua";
    private static readonly string[] ConfigurationFileNames = { "sorinkg.lua", "sorinxs.lua" };
    private const string DisabledConfigurationSuffix = ".disabled";

    public static string LauncherPath => Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, LauncherName);

    public static PackageDeploymentResult Deploy(Action<PackageDeploymentProgress>? progress = null)
    {
        var installDirectory = GetOrCreateInstallDirectory();
        var launcherPath = LauncherPath;
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException("找不到内置资源包。");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);
        var files = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name) && entry.FullName.StartsWith("AMacQ1156777787/", StringComparison.OrdinalIgnoreCase)).ToArray();
        progress?.Invoke(new PackageDeploymentProgress(0, files.Length, string.Empty));
        var extracted = new List<string>();
        for (var index = 0; index < files.Length; index++)
        {
            var entry = files[index];
            var relativePath = entry.FullName.Substring("AMacQ1156777787/".Length).Replace('/', Path.DirectorySeparatorChar);
            var destination = GetSafePath(installDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, true);
            extracted.Add(relativePath);
            progress?.Invoke(new PackageDeploymentProgress(index + 1, files.Length, relativePath));
        }
        File.WriteAllText(launcherPath, BuildLauncher(installDirectory), new UTF8Encoding(false));
        return new PackageDeploymentResult(extracted, Array.Empty<string>());
    }

    public static string? GetInstalledConfigurationPath(string fileName)
    {
        var directory = TryGetInstallDirectory();
        if (directory is null) return null;
        var path = Path.Combine(directory, fileName);
        return File.Exists(path) ? path : null;
    }

    public static string? GetInstallDirectory() => TryGetInstallDirectory();

    public static void RestoreRuntimeConfigurationFiles()
    {
        RenameRuntimeConfigurationFiles(disable: false);
    }

    public static void DisableRuntimeConfigurationFiles()
    {
        RenameRuntimeConfigurationFiles(disable: true);
    }

    private static string GetOrCreateInstallDirectory()
    {
        var existing = TryGetInstallDirectory();
        if (existing is not null) return existing;
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "Caches");
        Directory.CreateDirectory(root);
        var randomBytes = new byte[18];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(randomBytes);
        var name = Convert.ToBase64String(randomBytes).Replace('+', 'a').Replace('/', 'b').TrimEnd('=');
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(GetInstallRecordPath(), directory, new UTF8Encoding(false));
        return directory;
    }

    private static string? TryGetInstallDirectory()
    {
        var recordPath = GetInstallRecordPath();
        if (!File.Exists(recordPath)) return null;
        var directory = File.ReadAllText(recordPath).Trim();
        return Directory.Exists(directory) ? directory : null;
    }

    private static void RenameRuntimeConfigurationFiles(bool disable)
    {
        var directory = TryGetInstallDirectory();
        if (directory is null) return;

        foreach (var fileName in ConfigurationFileNames)
        {
            var activePath = Path.Combine(directory, fileName);
            var disabledPath = activePath + DisabledConfigurationSuffix;
            var sourcePath = disable ? activePath : disabledPath;
            var targetPath = disable ? disabledPath : activePath;
            if (File.Exists(sourcePath) && !File.Exists(targetPath)) File.Move(sourcePath, targetPath);
        }
    }

    private static string GetInstallRecordPath()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMacQ", "State");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "cache.dat");
    }

    private static string GetSafePath(string root, string relativePath)
    {
        var rootWithSeparator = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(rootWithSeparator, relativePath));
        if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("资源包包含无效路径，已停止部署。");
        return destination;
    }

    private static string BuildLauncher(string installDirectory)
    {
        const int key = 73;
        var normalizedDirectory = installDirectory.Replace('\\', '/').TrimEnd('/');
        var parentDirectory = normalizedDirectory.Substring(0, normalizedDirectory.LastIndexOf('/') + 1);
        var directoryName = normalizedDirectory.Substring(normalizedDirectory.LastIndexOf('/') + 1);
        var encodedParentDirectory = string.Join(",", parentDirectory.Select(character => ((int)character ^ key).ToString()));
        var encodedDirectoryName = string.Join(",", directoryName.Select(character => ((int)character ^ key).ToString()));
        var encodedModuleName = string.Join(",", "/ms.lua".Select(character => ((int)character ^ key).ToString()));
        return $@"local function b(a,c)local d={{[0]=0,[1]=1}};local e=1;local f=0;while a>0 or c>0 do local g=d[a%2]~=d[c%2] and 1 or 0;f=f+g*e;a=math.floor(a/2);c=math.floor(c/2);e=e*2 end;return f end
local function p(t,k)local r={{}}for i=1,#t do r[i]=string.char(b(t[i],k))end return table.concat(r)end
SorinQQ=ZuoZHEQQ1156777787
NSB=(load and loadstring or load)
QQ=""qq1156777787""
path=p({{{encodedParentDirectory}}},{key})
SorinName=p({{{encodedDirectoryName}}},{key})
ModuleName=p({{{encodedModuleName}}},{key})
dofile(path..SorinName..ModuleName)
";
    }
}
