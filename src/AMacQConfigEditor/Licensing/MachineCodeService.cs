using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AMacQConfigEditor.Licensing;

internal static class MachineCodeService
{
    public static string CurrentMachineCode
    {
        get
        {
            var machineGuid = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) as string;
            return string.IsNullOrWhiteSpace(machineGuid) ? "UNAVAILABLE" : Create(machineGuid!);
        }
    }

    public static string Create(string machineGuid)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(machineGuid.Trim()));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash) builder.Append(value.ToString("X2"));
        return builder.ToString();
    }
}
