using System;
using System.Security.Cryptography;
using System.Text;

namespace AMacQConfigEditor.Licensing;

internal static class LicenseValidator
{
    public const string PublicKeyXml = "<RSAKeyValue><Modulus>wxYZBOWyANnczaU8BqLDR0lv6IZM7KKF8UZaVtieEu4jt3T2pohGT9O34xr4p9WGcvqjVNAnObbZHyqlJqX9L4CgeOrSgFYkM2C81oUchlNyi+O5zNdpH/uqyt20N9T7TNxaTeYr1sHz0oUllYDtgK6Pb+J5BAxr/KjN0/iEENllpwW3EVR3a9eVVIERrV0uC20kxmkBoGAYBIRsXG/+5XCVGF6tmKfVJDq25LSICZKp56B9RAK9GTT1Krn7hRgfMKXFOcmibYh8zrkqwTO0Tn2peINXnzaPXem4iBz7QQAD2wvfPEElOAo6oNNgAU3kcfgeNaes7JPW9O4+duwbFQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

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
