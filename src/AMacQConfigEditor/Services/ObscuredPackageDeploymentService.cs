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

    /// <summary>文件被占用时（G HUB / 游戏正在读）的重试次数与间隔。</summary>
    private const int ConfigurationFileStateAttempts = 3;
    private const int ConfigurationFileStateRetryDelayMilliseconds = 120;

    public static string LauncherPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), LauncherName);
    private static string LegacyLauncherPath => Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, LauncherName);

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

    /// <summary>
    /// 清理上一次部署生成在 C 盘根目录的启动脚本。
    /// </summary>
    public static void CleanupPreviousDeployment()
    {
        try
        {
            if (File.Exists(LauncherPath)) File.Delete(LauncherPath);
            // 清理旧版本曾生成在 C 盘根目录的同名文件。
            if (File.Exists(LegacyLauncherPath)) File.Delete(LegacyLauncherPath);
        }
        catch (IOException)
        {
            // 文件正在被游戏或 G HUB 使用时，不影响程序启动。
        }
        catch (UnauthorizedAccessException)
        {
            // 权限不足时不影响程序启动。
        }
    }

    /// <summary>恢复（启用）运行期配置文件，返回未能处理的文件名。</summary>
    public static IReadOnlyList<string> RestoreRuntimeConfigurationFiles() => RenameRuntimeConfigurationFiles(disable: false);

    /// <summary>
    /// 禁用运行期配置文件，返回未能处理的文件名。
    /// 关闭程序后这两个文件处于禁用状态，鼠标宏便无法再读取它们。
    /// </summary>
    public static IReadOnlyList<string> DisableRuntimeConfigurationFiles() => RenameRuntimeConfigurationFiles(disable: true);

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

    private static IReadOnlyList<string> RenameRuntimeConfigurationFiles(bool disable)
    {
        var directory = TryGetInstallDirectory();
        if (directory is null) return Array.Empty<string>();

        var failures = new List<string>();
        foreach (var fileName in ConfigurationFileNames)
        {
            var activePath = Path.Combine(directory, fileName);
            var disabledPath = activePath + DisabledConfigurationSuffix;
            if (!TryApplyConfigurationFileState(activePath, disabledPath, disable)) failures.Add(fileName);
        }

        return failures;
    }

    /// <summary>
    /// 把一对"活动 / 禁用"配置文件调整到目标状态，并清掉多余的那一份。
    ///
    /// 这里刻意不做成"目标不存在才改名"：那样一旦两份并存（例如部署时重新解压出活动文件、
    /// 而旧的禁用副本还在），两个方向都会永远失效，禁用保护形同虚设。现在的规则是：
    /// 活动文件始终是较新的那一份（编辑器与部署都写它），因此启用时以它为准、丢弃禁用副本；
    /// 禁用时把活动文件改名为禁用副本。
    ///
    /// 返回 false 表示尝试若干次后仍然失败（通常是文件被 G HUB 或游戏占用）。
    /// </summary>
    internal static bool TryApplyConfigurationFileState(string activePath, string disabledPath, bool disable)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                ApplyConfigurationFileStateOnce(activePath, disabledPath, disable);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt >= ConfigurationFileStateAttempts) return false;
                Thread.Sleep(ConfigurationFileStateRetryDelayMilliseconds);
            }
        }
    }

    private static void ApplyConfigurationFileStateOnce(string activePath, string disabledPath, bool disable)
    {
        if (disable)
        {
            if (!File.Exists(activePath)) return; // 已经处于禁用状态
            if (File.Exists(disabledPath)) File.Delete(disabledPath);
            File.Move(activePath, disabledPath);
            return;
        }

        if (File.Exists(activePath))
        {
            // 活动文件较新，保留它，只清掉可能残留的禁用副本
            if (File.Exists(disabledPath)) File.Delete(disabledPath);
            return;
        }

        if (File.Exists(disabledPath)) File.Move(disabledPath, activePath);
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


