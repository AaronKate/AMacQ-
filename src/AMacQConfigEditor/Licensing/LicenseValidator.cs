using System;
using System.Security.Cryptography;
using System.Text;

namespace AMacQConfigEditor.Licensing;

internal static class LicenseValidator
{
    public const string PublicKeyXml = "<RSAKeyValue><Modulus>3n9J5b/PyGSTA0sBpVf7wjwXAlSMCVGrMb7UgkEzgLoGlp5OiJek1Jl3IfatlSNoH0hIIKVxU+CjbRpKTvLlqGHYDf3TlP+kKBPSN4J/bju565OWaWzmc8PPUCdx6AqD3pp7YAloaq2flPg+Jxd1Zx1dzPlQYCLO6auO9CKWw7+w2kyX8Meo+IXr6XjobI4NhZMyiJzeuyNxgrYetHglRYzaKYyyJSzBqiqB82mIYiBo7mPnWeyVVl2R5GnQeCkdHzmI0yxcwamO9SrqHGqW6M1PmC50hmRblu3Si/ET91VlRCHt7QcijG7KjUailuTPBlKmFAK9/XwqyUdLnG4Qm3HbZu/qvaY9s8JdjaPWFWqMnFZxVcnTMCaPfhp+Al/dSGgiLsxYTEYmrc9719REaNvEEbnQxDpnvBXYrBlBpij0G/O/3GxCEL+RtvORzEnyxUd6DIhegVla5SBn5jnnn/HJQY4TvZl69n14IpR3CdF8rgp18miOskEmns74DHTgLh7r4eAqDaSFHRGx7Rnx+NMmBcYfraGKitiQamcZurC6WH4vMd2M+P+271DYg0/X0/UHBx78Hwb8ujCxohH6aI+AYgVcbdD7+Mqrr0TQ1U5Rj8R3EO9vgxObawGD04BMIW0lHj367wkDzenDVbV7D9qNiE/wvwaARS0AAQP2tTk=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

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
