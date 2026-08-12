using System;
using System.Windows;

namespace AMacQLicenseGenerator;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
            return LicenseGenerator.TryGenerateFromArguments(args, out var message) ? 0 : ShowCommandLineError(message);

        var application = new Application();
        application.Run(new MainWindow());
        return 0;
    }

    private static int ShowCommandLineError(string message)
    {
        MessageBox.Show(message + "\n\n用法：AMacQLicenseGenerator <private-key.xml> <output-license.json> <machine-code> perpetual|expires [yyyy-MM-dd]", "授权签发失败", MessageBoxButton.OK, MessageBoxImage.Error);
        return 1;
    }
}
