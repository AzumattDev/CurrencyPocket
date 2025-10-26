using BepInEx.Bootstrap;

namespace CurrencyPocket.Compatibility;

public class RapidLoadoutsCompat
{
    public static void Init()
    {
        if (!Chainloader.PluginInfos.TryGetValue(RapidLoadoutsGUID, out PluginInfo rapidLoadoutsInfo)) return;
        if (rapidLoadoutsInfo != null && rapidLoadoutsInfo.Instance)
        {
            // RapidLoadouts is loaded
            CurrencyPocketPlugin.instance._harmony.PatchAll(typeof(RapidLoadoutsCompat));
        }
    }

    [HarmonyPatch("RapidLoadouts.UI.PurchasableLoadoutGui, RapidLoadouts", "GetPlayerCoins"), HarmonyPostfix]
    public static void GetPlayerCoins(ref int __result, ref ItemDrop ___m_coinPrefab)
    {
        if (!Player.m_localPlayer || ___m_coinPrefab == null) return;
        if (___m_coinPrefab.m_itemData.m_shared.m_name == CoinToken)
        {
            __result += Player.m_localPlayer.m_customData.TryGetValue(CoinCountCustomData, out string coinCount) ? int.Parse(coinCount) : 0;
        }
    }
}