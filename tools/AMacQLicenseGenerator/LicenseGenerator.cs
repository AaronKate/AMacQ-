using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace AMacQLicenseGenerator;

internal static class LicenseGenerator
{
    public static bool TryGenerateFromArguments(string[] args, out string message)
    {
        if (args.Length is < 4 or > 5)
        {
            message = "参数数量不正确。";
            return false;
        }

        DateTime? expiresUtc = null;
        var mode = args[3].Trim().ToLowerInvariant();
        if (mode == "expires")
        {
            if (args.Length != 5 || !DateTime.TryParseExact(args[4], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                message = "到期授权需要 yyyy-MM-dd 格式的日期。";
                return false;
            }
            expiresUtc = DateTime.SpecifyKind(date.Date.AddDays(1).AddMilliseconds(-1), DateTimeKind.Utc);
        }
        else if (mode != "perpetual" || args.Length != 4)
        {
            message = "授权类型必须为 perpetual 或 expires。";
            return false;
        }

        return TryGenerate(args[0], args[1], args[2], mode, expiresUtc, out message);
    }

    public static bool TryGenerate(string privateKeyPath, string outputLicensePath, string machineCode, string mode, DateTime? expiresUtc, out string message)
    {
        machineCode = machineCode.Trim().ToUpperInvariant();
        if (machineCode.Length == 0) { message = "请填写机器码。"; return false; }
        if (!File.Exists(privateKeyPath)) { message = "找不到私钥文件。"; return false; }
        if (string.IsNullOrWhiteSpace(outputLicensePath)) { message = "请选择许可证保存位置。"; return false; }

        try
        {
            using var rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(File.ReadAllText(privateKeyPath));
            var unsigned = new LicenseDocument("1", machineCode, mode, expiresUtc, string.Empty);
            var signature = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(unsigned.ToCanonicalPayload()), CryptoConfig.MapNameToOID("SHA256")));
            var license = new LicenseDocument(unsigned.Version, unsigned.MachineCode, unsigned.Mode, unsigned.ExpiresUtc, signature);
            File.WriteAllText(outputLicensePath, license.ToJson(), new UTF8Encoding(false));
            message = "许可证已生成：\n" + outputLicensePath;
            return true;
        }
        catch (Exception exception)
        {
            message = "许可证生成失败：" + exception.Message;
            return false;
        }
    }
}

[DataContract]
internal sealed class LicenseDocument
{
    public LicenseDocument(string version, string machineCode, string mode, DateTime? expiresUtc, string signature)
    { Version = version; MachineCode = machineCode; Mode = mode; ExpiresUtc = expiresUtc; Signature = signature; }
    [DataMember(Name = "version", Order = 1)] public string Version { get; private set; }
    [DataMember(Name = "machineCode", Order = 2)] public string MachineCode { get; private set; }
    [DataMember(Name = "mode", Order = 3)] public string Mode { get; private set; }
    [DataMember(Name = "expiresUtc", Order = 4)] public DateTime? ExpiresUtc { get; private set; }
    [DataMember(Name = "signature", Order = 5)] public string Signature { get; private set; }
    public string ToCanonicalPayload() => $"{Version}\n{MachineCode}\n{Mode}\n{ExpiresUtc?.ToUniversalTime().ToString("O") ?? string.Empty}";
    public string ToJson()
    {
        var serializer = new DataContractJsonSerializer(typeof(LicenseDocument));
        using var stream = new MemoryStream(); serializer.WriteObject(stream, this);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
