namespace AMacQConfigEditor;

/// <summary>
/// 枪械的三条按键槽位。配置文件里依次对应主宏（无修饰键）、Alt 槽位与 Ctrl 槽位，
/// 每个"槽位 + 按键值"组合在全部枪械中只能被一把枪占用。
/// </summary>
public enum WeaponBindingSlot
{
    Primary,
    Alt,
    Ctrl
}
