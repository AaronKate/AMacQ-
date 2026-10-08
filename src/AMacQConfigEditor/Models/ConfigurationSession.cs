using AMacQConfigEditor.Services;

namespace AMacQConfigEditor.Models;

public sealed class ConfigurationSession
{
    public ConfigurationSession(ConfigFile keyBindings, ConfigFile sensitivity)
    {
        KeyBindings = keyBindings;
        Sensitivity = sensitivity;
        KeyBindingsDocument = LuaConfigService.CreateDocument(keyBindings.Content);
        SensitivityDocument = LuaConfigService.CreateDocument(sensitivity.Content);
        Weapons = LuaConfigService.GetPrimaryWeapons(keyBindings.Content);
    }

    public ConfigFile KeyBindings { get; }
    public ConfigFile Sensitivity { get; }

    /// <summary>
    /// 按键/灵敏度配置的内存视图。整份文档只在会话建立时解析一次，界面切换枪械、
    /// 刷新列表、保存时的读取都走缓存，不再反复扫描全文。
    /// </summary>
    public LuaConfigService.LuaDocument KeyBindingsDocument { get; }
    public LuaConfigService.LuaDocument SensitivityDocument { get; }

    public IReadOnlyList<string> Weapons { get; }
}
