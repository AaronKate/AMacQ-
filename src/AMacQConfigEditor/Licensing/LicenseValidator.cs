using System;
using System.Security.Cryptography;
using System.Text;

namespace AMacQConfigEditor.Licensing;

internal static class LicenseValidator
{
    public const string PublicKeyXml = "<RSAKeyValue><Modulus>0i2+PzExJWQLTHDzTlCpaA0BjEGcq3UcVdzEQdjs16OYAixE6NKnanlIzwm3msRqb/HQDUOSBXe+tx3K5DXgTqykFGw4xwWMzCmhyDPiVByg0rBQwAa+MhY9sxFyRjFfGezB/hyQvPTczOBdLmcGqa998N6tdAVTWJQB5xV1o9/Ou32yCmqID8+QozdMY6SYS0i5h8YhbWTaMupg2eHl8tE5S2v1dYeY9Lw8oabTBHBKoVHccjWgmC2XiiuIMlv5LMlvqeNz9pSjmo+UTXZK6drCh7iPF4PfQvsERNQERuMsaBe43HfF90sc4IDlnXXJ+f5P2zHb6sTvUt41dUINm+CHMUQ7x7X/ntYWT47IX0oCE3QtTxMHH0LJ8HBq1yZU/CCEZn/+/ow6MGJmZHqENmrqCgX9RiQNTRx6nJTX0tTVyydsVVhMKPsALF/8X2X76fRqiveNqTI4VoK1fmCp0LWpaiTsf+8HY23dyvfWiNYdlQg+OfA4iIoqUo22aACMYG8l/ngpZjsHxt9KTYYCLpA4eMHX4WWE/+WV2BN9+da8F0qg7Lf2dvEd53lzyk+UkT++81Xj9pvCvyzHpZOUX6jheQbch9DUUjYCI0G80gFfADU/5P7DFPPuXbx5CJlFu2DWw3TH/Eo8JmYZhFK3u/KiEZJYa9g9HdKloYfFtpE=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    public static LicenseValidationResult Validate(string licenseJson, string machineCode, DateTime utcNow, string publicKeyXml)
    {
        var license = LicenseDocument.FromJson(licenseJson);
        if (license is null || license.Version != "1" || string.IsNullOrWhiteSpace(license.Signature)) return LicenseValidationResult.Invalid("许可证格式无效。");
        if (license.Mode is not "perpetual" and not "expires") return LicenseValidationResult.Invalid("许可证授权模式无效。");
        if (license.Mode == "expires" && license.ExpiresUtc is null) return LicenseValidationResult.Invalid("许可证缺少到期时间。");
        if (!string.Equals(license.MachineCode, machineCode, StringComparison.Ordinal)) return LicenseValidationResult.Invalid("许可证不属于此设备。");
        if (license.ExpiresUtc is { } expiresUtc && utcNow.ToUniversalTime() > expiresUtc.ToUniversalTime()) return LicenseValidationResult.Invalid("许可证已过期。");

        try
        {
            using var rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publicKeyXml);
            var signature = Convert.FromBase64String(license.Signature);
            var valid = rsa.VerifyData(Encoding.UTF8.GetBytes(license.ToCanonicalPayload()), CryptoConfig.MapNameToOID("SHA256"), signature);
            return valid ? LicenseValidationResult.Valid() : LicenseValidationResult.Invalid("许可证签名无效。");
        }
        catch (Exception)
        {
            return LicenseValidationResult.Invalid("许可证签名无效。");
        }
    }

    public static bool IsSignedLicenseExpired(string licenseJson, string machineCode, DateTime utcNow, string publicKeyXml)
    {
        var license = LicenseDocument.FromJson(licenseJson);
        if (license is null || license.Version != "1" || license.Mode != "expires" || license.ExpiresUtc is null || string.IsNullOrWhiteSpace(license.Signature)) return false;
        if (!string.Equals(license.MachineCode, machineCode, StringComparison.Ordinal)) return false;

        try
        {
            using var rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publicKeyXml);
            var signature = Convert.FromBase64String(license.Signature);
            var signed = rsa.VerifyData(Encoding.UTF8.GetBytes(license.ToCanonicalPayload()), CryptoConfig.MapNameToOID("SHA256"), signature);
            return signed && utcNow.ToUniversalTime() > license.ExpiresUtc.Value.ToUniversalTime();
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal sealed record LicenseValidationResult(bool IsValid, string? Error)
{
    public static LicenseValidationResult Valid() => new(true, null);
    public static LicenseValidationResult Invalid(string error) => new(false, error);
}
