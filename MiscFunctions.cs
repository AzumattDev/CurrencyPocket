using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace CurrencyPocket;

public class MiscFunctions
{
    public static string GetPrefabName(string name)
    {
        char[] anyOf = { '(', ' ' };
        int length = name.IndexOfAny(anyOf);
        return length < 0 ? name : name.Substring(0, length);
    }

    internal static GameObject? GetItemPrefabFromGameObject(ItemDrop itemDropComponent, GameObject inputGameObject)
    {
        GameObject? itemPrefab = ObjectDB.instance.GetItemPrefab(GetPrefabName(inputGameObject.name));
        itemDropComponent.m_itemData.m_dropPrefab = itemPrefab;
        return itemPrefab != null ? itemPrefab : null;
    }

    internal static bool CheckItemDropIntegrity(ItemDrop itemDropComp)
    {
        return itemDropComp.m_itemData?.m_shared != null;
    }

    internal static void ProcessRequirements(Piece.Requirement[] requirements, int qualityLevel, Player instance, int itemQuality)
    {
        foreach (Piece.Requirement requirement in requirements)
        {
            if (!IsValidRequirement(requirement)) continue;
            int totalRequirement = requirement.GetAmount(qualityLevel);
            if (totalRequirement <= 0) continue;

            string reqName = requirement.m_resItem.m_itemData.m_shared.m_name;
            if (reqName == CurrencyPocket.CoinToken)
            {
                int coins = instance.m_customData.TryGetValue(CurrencyPocket.CoinCountCustomData, out string coinCount) ? int.Parse(coinCount) : 0;
                // Remove coins from player custom data in the amount
                UpdatePlayerCustomData(coins - (Math.Min(coins, totalRequirement)), instance);
                CurrencyPocket.UpdatePocketUI();
            }
        }
    }

    private static bool IsValidRequirement(Piece.Requirement requirement)
    {
        return requirement.m_resItem && requirement.m_resItem.m_itemData is { m_shared: not null };
    }

    internal static void UpdatePlayerCustomData(int coinCount, Player? player = null)
    {
        if (player == null)
        {
            player = Player.m_localPlayer;
        }

        if (player != null)
        {
            player.m_customData[CurrencyPocket.CoinCountCustomData] = coinCount.ToString();
        }
    }

    internal static int GetPlayerCoinsFromCustomData()
    {
        Player player = Player.m_localPlayer;

        if (player != null && player.m_customData.TryGetValue(CurrencyPocket.CoinCountCustomData, out string coinCount))
        {
            return int.Parse(coinCount);
        }

        return 0;
    }

    internal static void ExtractCoins()
    {
        // Logic to extract coins from the pocket to the inventory
        Player player = Player.m_localPlayer;
        if (player != null && GetPlayerCoinsFromCustomData() > 0)
        {
            GameObject? coins = ObjectDB.instance.GetItemPrefab("Coins");
            InventoryGuiOnSplitOkPatch.throwAwayInventory = new Inventory(CurrencyPocket.CoinCountCustomData, coins.GetComponent<ItemDrop>().m_itemData.GetIcon(), 1, 1);
            InventoryGuiOnSplitOkPatch.throwAwayInventory.AddItem(coins, GetPlayerCoinsFromCustomData());
            InventoryGui.instance.ShowSplitDialog(InventoryGuiOnSplitOkPatch.throwAwayInventory.m_inventory.FirstOrDefault(), InventoryGuiOnSplitOkPatch.throwAwayInventory);
            CurrencyPocket.CoinExtractionInProgress = true;
            // TODO: Might add this as a config later, to extract all coins.
            /*if (player.GetInventory().CanAddItem(coins, _coinCount))
            {
                player.GetInventory().AddItem(coins, _coinCount);
                _coinCount = 0;
                UpdatePocketUI();
            }
            else
            {
                 player.Message(MessageHud.MessageType.Center, "$inventory_full");
            }*/
        }
    }
}