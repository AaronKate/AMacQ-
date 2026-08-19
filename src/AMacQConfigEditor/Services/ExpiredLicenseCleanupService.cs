namespace AMacQConfigEditor.Services;

internal static class ExpiredLicenseCleanupService
{
    public static void DisableRuntimeConfigurationFiles()
    {
        ObscuredPackageDeploymentService.DisableRuntimeConfigurationFiles();
    }
}
