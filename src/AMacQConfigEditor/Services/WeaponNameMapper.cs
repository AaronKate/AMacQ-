using System;
using System.Collections.Generic;

namespace AMacQConfigEditor.Services;

/// <summary>
/// 将配置文件中的内部枪械标识映射为三角洲行动 S11 使用的显示名称。
/// 配置键仍使用内部标识；未收录的标识直接显示原名。
/// </summary>
public static class WeaponNameMapper
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 突击步枪 / 战斗步枪
            ["AK12"] = "AK-12",
            ["AKM"] = "AKM",
            ["AR57"] = "AR-57",
            ["ASVAL"] = "AS Val",
            ["AUG"] = "AUG",
            ["CAR15"] = "CAR-15",
            ["G3"] = "G3",
            ["K416"] = "K416",
            ["K437"] = "K437",
            ["KC17"] = "KC17",
            ["M4A1"] = "M4A1",
            ["MCX"] = "MCX LT",
            ["MDR"] = "MDR",
            ["MK47"] = "MK47",
            ["QBZ"] = "QBZ95-1",
            ["RM277"] = "RM277",
            ["SCAR"] = "SCAR-H",
            ["SG552"] = "SG 552",
            ["TJ191"] = "腾龙突击步枪",
            ["ASH"] = "ASh-12",

            // 冲锋枪
            ["MK4"] = "MK4",
            ["MP5"] = "MP5",
            ["MP7"] = "MP7",
            ["M7"] = "M7",
            ["QCQ17"] = "QCQ171",
            ["SR3M"] = "SR-3M",
            ["TOM"] = "汤姆逊",
            ["UZI"] = "UZI",
            ["Vector"] = "Vector",
            ["YeNiu"] = "野牛",
            ["Bizon"] = "野牛",
            ["Vityaz"] = "维克托兹",

            // 轻机枪
            ["M250"] = "M250",
            ["PKM"] = "PKM",
            ["QJB201"] = "QJB201",

            // 射手步枪 / 狙击步枪
            ["M14"] = "M14",
            ["PTR32"] = "PTR-32",
            ["SVCH"] = "SVCh",
            ["M700"] = "M700",
            ["SV98"] = "SV-98",
            ["SR25"] = "SR-25",
            ["AWM"] = "AWM",
            ["M82"] = "M82",

            // 手枪 / 霰弹枪
            ["M1911"] = "M1911",
            ["G17"] = "G17",
            ["G18"] = "G18",
            ["93R"] = "93R",
            ["Revolver"] = "左轮",
            ["S12K"] = "S12K",
            ["M870"] = "M870",
            ["M1014"] = "M1014",

            // 兼容配置中可能出现的带连字符写法
            ["AKS74"] = "AKS-74",
            ["AKS-74"] = "AKS-74",
            ["CI19"] = "CI-19",
            ["CI-19"] = "CI-19",
            ["SV-98"] = "SV-98",
            ["SR-25"] = "SR-25",
        };

    public static string GetDisplayName(string? internalName)
    {
        if (string.IsNullOrWhiteSpace(internalName)) return string.Empty;
        var key = internalName!;
        return DisplayNames.TryGetValue(key, out var displayName)
            ? displayName
            : key;
    }
}

