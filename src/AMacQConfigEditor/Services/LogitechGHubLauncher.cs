using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AMacQConfigEditor.Services;

internal sealed class LogitechGHubLauncher
{
    public const string GHubDownloadUrl = "https://download01.logi.com/web/ftp/pub/techsupport/gaming/lghub_installer.exe";
    private const string FailureMessage = "未检测到 Logitech G HUB，已为您打开浏览器下载页面，请安装后重新部署。";
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string> _startProcess;

    public LogitechGHubLauncher(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Action<string> startProcess)
    {
        _getEnvironmentVariable = getEnvironmentVariable;
        _fileExists = fileExists;
        _startProcess = startProcess;
    }

    public static LogitechGHubLaunchResult TryLaunchInstalledGHub()
    {
        return new LogitechGHubLauncher(
            Environment.GetEnvironmentVariable,
            File.Exists,
            path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }))
            .TryLaunch();
    }

    public static string AppendFailureMessage(string deploymentMessage, LogitechGHubLaunchResult launchResult)
    {
        return launchResult.IsLaunched
            ? deploymentMessage
            : $"{deploymentMessage}{Environment.NewLine}{launchResult.FailureMessage}";
    }

    public static void OpenDownloadPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(GHubDownloadUrl) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // 打开浏览器失败时静默处理，不影响部署结果
        }
    }

    public LogitechGHubLaunchResult TryLaunch()
    {
        try
        {
            var executablePath = new[]
            {
                _getEnvironmentVariable("ProgramFiles"),
                _getEnvironmentVariable("ProgramFiles(x86)")
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.Combine(path!, "LGHUB", "lghub.exe"))
            .FirstOrDefault(_fileExists);

            if (executablePath is null)
                return new LogitechGHubLaunchResult(false, FailureMessage);

            _startProcess(executablePath);
            return new LogitechGHubLaunchResult(true, null);
        }
        catch (Exception)
        {
            return new LogitechGHubLaunchResult(false, FailureMessage);
        }
    }
}

internal sealed record LogitechGHubLaunchResult(bool IsLaunched, string? FailureMessage);
