using System.Windows;
using AMacQConfigEditor.Licensing;
using AMacQConfigEditor.Services;

namespace AMacQConfigEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

#if AUTHOR_EDITION
        new MainWindow().Show();
        return;
#else
        RemoveExpiredRuntimeConfiguration();
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

    private static bool IsLicenseValid()
    {
        var license = LicenseStore.Load();
        if (string.IsNullOrWhiteSpace(license)) return false;
        return LicenseValidator.Validate(license!, MachineCodeService.CurrentMachineCode, System.DateTime.UtcNow, LicenseValidator.PublicKeyXml).IsValid;
    }

    private static void RemoveExpiredRuntimeConfiguration()
    {
        var licenseJson = LicenseStore.Load();
        if (!string.IsNullOrWhiteSpace(licenseJson)
            && LicenseValidator.IsSignedLicenseExpired(licenseJson!, MachineCodeService.CurrentMachineCode, System.DateTime.UtcNow, LicenseValidator.PublicKeyXml))
        {
            ExpiredLicenseCleanupService.RemoveRuntimeConfigurationFiles();
        }
    }
}
