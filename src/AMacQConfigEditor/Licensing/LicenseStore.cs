using System;
using System.IO;

namespace AMacQConfigEditor.Licensing;

internal static class LicenseStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMacQConfigEditor");
    public static string LicensePath => Path.Combine(DirectoryPath, "license.json");

    public static string? Load() => File.Exists(LicensePath) ? File.ReadAllText(LicensePath) : null;

    public static void Import(string sourcePath)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.Copy(sourcePath, LicensePath, true);
    }
}
