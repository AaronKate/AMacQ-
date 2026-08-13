using System.IO;

namespace AMacQConfigEditor.Services;

internal static class ExpiredLicenseCleanupService
{
    public static void RemoveRuntimeConfigurationFiles()
    {
        var directory = ObscuredPackageDeploymentService.GetInstallDirectory();
        if (directory is null) return;

        foreach (var fileName in new[] { "sorinkg.lua", "sorinxs.lua" })
        {
            var path = Path.Combine(directory, fileName);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
