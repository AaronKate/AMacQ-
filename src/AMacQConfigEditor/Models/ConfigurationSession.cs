using AMacQConfigEditor.Services;

namespace AMacQConfigEditor.Models;

public sealed class ConfigurationSession
{
    public ConfigurationSession(ConfigFile keyBindings, ConfigFile sensitivity)
    {
        KeyBindings = keyBindings;
        Sensitivity = sensitivity;
        Weapons = LuaConfigService.GetPrimaryWeapons(keyBindings.Content);
    }

    public ConfigFile KeyBindings { get; }
    public ConfigFile Sensitivity { get; }
    public IReadOnlyList<string> Weapons { get; }
}
