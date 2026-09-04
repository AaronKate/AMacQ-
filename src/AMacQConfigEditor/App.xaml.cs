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
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!IsRunningAsAdministrator())
        {
            if (PromptForAdministratorRestart()) return;
            Shutdown();
            return;
        }

#if AUTHOR_EDITION
        ShowMainWindow();
        return;
#else
        RemoveInvalidLicenseAndExpiredRuntimeConfiguration();
        if (IsLicenseValid())
        {
            ShowMainWindow();
            return;
        }

        var licenseWindow = new LicenseWindow();
        if (licenseWindow.ShowDialog() == true) ShowMainWindow();
        else Shutdown();
#endif
    }

    private static void ShowMainWindow()
    {
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        Current.MainWindow = new MainWindow();
        Current.MainWindow.Show();
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private bool PromptForAdministratorRestart()
    {
        var prompt = new AdminPromptWindow();
        if (prompt.ShowDialog() != true) return false;

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
            var failurePrompt = new AdminPromptWindow();
            failurePrompt.ShowRestartFailure();
            failurePrompt.ShowDialog();
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
