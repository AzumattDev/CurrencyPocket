using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace CurrencyPocket.Compatibility;

public class ExtraSlots
{
    public static void FuckOff()
    {
        if (!Chainloader.PluginInfos.TryGetValue("shudnal.ExtraSlots", out var ShitSlots)) return;
        // Dude changes the locations of armor & weight panels on the player inventory even if the inventory isn't reduced like he says in the config definition.
        // "If regular inventory height is reduced panels have to be moved in appropriate position. Disable this config if other mod handles it."
        bool tryGetEntry = ShitSlots.Instance.Config.TryGetEntry("Mods compatibility - Reduced inventory size", "Move Armor and Weight panels", out ConfigEntry<bool>? entry);
        if (!tryGetEntry || !entry.Value) return;
        entry.Value = false;
        ShitSlots.Instance.Config.Save();
    }
}