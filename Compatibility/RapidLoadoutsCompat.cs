using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace CurrencyPocket.Compatibility;

public class RapidLoadoutsCompat
{
    public static void Init()
    {
        if (Chainloader.PluginInfos.TryGetValue("Azumatt.RapidLoadouts", out PluginInfo rapidLoadoutsInfo))
        {
            if (rapidLoadoutsInfo != null && rapidLoadoutsInfo.Instance != null)
            {
                // RapidLoadouts is loaded
                CurrencyPocketPlugin.instance._harmony.PatchAll(typeof(RapidLoadoutsCompat));
            }
        }
    }

    [HarmonyPatch("RapidLoadouts.UI.PurchasableLoadoutGui, RapidLoadouts", "GetPlayerCoins"), HarmonyPostfix]
    public static void GetPlayerCoins(ref int __result, ref ItemDrop ___m_coinPrefab)
    {
        if (Player.m_localPlayer != null && ___m_coinPrefab != null)
        {
            if (___m_coinPrefab.m_itemData.m_shared.m_name == CurrencyPocket.CoinToken)
            {
                __result += Player.m_localPlayer.m_customData.TryGetValue(CurrencyPocket.CoinCountCustomData, out string coinCount) ? int.Parse(coinCount) : 0;
            }
        }
    }
}