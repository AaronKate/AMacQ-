using System.Windows;
using AMacQConfigEditor.Licensing;

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
}
