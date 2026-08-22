using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using AMacQConfigEditor.Licensing;
using AMacQConfigEditor.Services;

namespace AMacQConfigEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

        if (!IsRunningAsAdministrator() && PromptForAdministratorRestart()) return;

#if AUTHOR_EDITION
        new MainWindow().Show();
        return;
#else
        RemoveInvalidLicenseAndExpiredRuntimeConfiguration();
        if (IsLicenseValid())
        {
            new MainWindow().Show();
            return;
        }

        var licenseWindow = new LicenseWindow();
        if (licenseWindow.ShowDialog() == true) new MainWindow().Show();
        else Shutdown();
#endif
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private bool PromptForAdministratorRestart()
    {
        var result = MessageBox.Show(
            "建议以管理员模式启动，以确保部署和配置操作能够正常完成。\n\n是否现在以管理员权限重新启动？",
            "建议使用管理员模式",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return false;

        try
        {
            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath)) return false;

            Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true, Verb = "runas" });
            Shutdown();
            return true;
        }
        catch
        {
            MessageBox.Show("未能以管理员权限重新启动，程序将以当前权限继续运行。", "启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private static bool IsLicenseValid()
    {
        var license = LicenseStore.Load();
        if (string.IsNullOrWhiteSpace(license)) return false;
        return LicenseValidator.Validate(license!, MachineCodeService.CurrentMachineCode, System.DateTime.UtcNow, LicenseValidator.PublicKeyXml).IsValid;
    }

    private static void RemoveInvalidLicenseAndExpiredRuntimeConfiguration()
    {
        var licenseJson = LicenseStore.Load();
        if (string.IsNullOrWhiteSpace(licenseJson)) return;

        var machineCode = MachineCodeService.CurrentMachineCode;
        var utcNow = System.DateTime.UtcNow;
        var validation = LicenseValidator.Validate(licenseJson!, machineCode, utcNow, LicenseValidator.PublicKeyXml);
        if (!validation.IsValid)
        {
            if (LicenseValidator.IsSignedLicenseExpired(licenseJson!, machineCode, utcNow, LicenseValidator.PublicKeyXml))
            {
                ExpiredLicenseCleanupService.DisableRuntimeConfigurationFiles();
            }

            LicenseStore.Delete();
        }
    }
}
