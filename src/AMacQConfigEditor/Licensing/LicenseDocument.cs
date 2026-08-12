using System;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;

namespace AMacQConfigEditor.Licensing;

[DataContract]
internal sealed class LicenseDocument
{
    public LicenseDocument(string version, string machineCode, string mode, DateTime? expiresUtc, string signature)
    {
        Version = version;
        MachineCode = machineCode;
        Mode = mode;
        ExpiresUtc = expiresUtc;
        Signature = signature;
    }

    [DataMember(Name = "version", Order = 1)] public string Version { get; private set; }
    [DataMember(Name = "machineCode", Order = 2)] public string MachineCode { get; private set; }
    [DataMember(Name = "mode", Order = 3)] public string Mode { get; private set; }
    [DataMember(Name = "expiresUtc", Order = 4)] public DateTime? ExpiresUtc { get; private set; }
    [DataMember(Name = "signature", Order = 5)] public string Signature { get; private set; }

    public string ToCanonicalPayload() => $"{Version}\n{MachineCode}\n{Mode}\n{ExpiresUtc?.ToUniversalTime().ToString("O") ?? string.Empty}";

    public string ToJson()
    {
        var serializer = new DataContractJsonSerializer(typeof(LicenseDocument));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, this);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static LicenseDocument? FromJson(string json)
    {
        try
        {
            var serializer = new DataContractJsonSerializer(typeof(LicenseDocument));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return serializer.ReadObject(stream) as LicenseDocument;
        }
        catch (SerializationException)
        {
            return null;
        }
    }
}
