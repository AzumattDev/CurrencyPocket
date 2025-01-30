using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static CurrencyPocket.MiscFunctions;
using static CurrencyPocket.Constants;

namespace CurrencyPocket;

public class CurrencyPocket
{
    internal static bool CoinExtractionInProgress = false;

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    public static class InventoryGuiUpdatePatch
    {
        public static Button ExtractButton = null!;
        public static GameObject pocketUI = null!;
        public static Sprite coinSprite = null!;


        [HarmonyAfter(JewelcraftingGUID)]
        static void Postfix(InventoryGui __instance)
        {
            if (pocketUI == null)
            {
                CreatePocketUI(__instance);
            }

            // Ran once when the inventory is opened
            if (ExtractButton == null && pocketUI != null)
            {
                CreateButton(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    static class InventoryGuiShowPatch
    {
        static void Prefix()
        {
            UpdatePocketUI();
        }

        private static GameObject? cached;
        private static Coroutine? coroutine;

        [HarmonyPriority(Priority.VeryLow)]
        private static void Postfix(InventoryGui __instance)
        {
            if (cached)
            {
                if (coroutine != null)
                    __instance.StopCoroutine(coroutine);
                return;
            }

            cached = __instance.gameObject;

            IEnumerator WaitOneFrame()
            {
                yield return null;
                Transform inv = __instance.m_player.transform;
                for (int i = 0; i < inv.childCount; ++i)
                {
                    Transform child = inv.GetChild(i);
                    if (child.name is ArmorName or WeightName or JewelcraftingSynergyName or CoinPocketUIName or TrashButtonName)
                    {
                        RectTransform? rect = child.gameObject.GetComponent<RectTransform>();
                        if (rect != null)
                        {
                            switch (child.name)
                            {
                                case CoinPocketUIName when !IsOverlappingUIModInstalled():
                                    rect.anchoredPosition += new Vector2(0, -45);
                                    break;
                                case WeightName when !IsOverlappingUIModInstalled():
                                    // Do nothing
                                    break;
                                default:
                                    rect.anchoredPosition += new Vector2(0, 45);
                                    break;
                            }
                        }
                    }

                    if (Chainloader.PluginInfos.ContainsKey(RandyQuickslots) || Chainloader.PluginInfos.ContainsKey(AzuEPIGUID))
                    {
                        if (child.name is SortInventoryButton or RestockAreaButton or QuickStackAreaButton or FavoritingToggleButton)
                        {
                            RectTransform? rect = child.gameObject.GetComponent<RectTransform>();
                            if (rect != null)
                            {
                                if (child.name == FavoritingToggleButton)
                                    rect.anchoredPosition += new Vector2(0, 20);
                                else if (!Chainloader.PluginInfos.ContainsKey(RandyQuickslots) && Chainloader.PluginInfos.ContainsKey(AzuEPIGUID))
                                    rect.anchoredPosition += new Vector2(0, -15);
                                else
                                {
                                    rect.anchoredPosition += new Vector2(0, -30);
                                }
                            }
                        }
                    }
                }
            }

            coroutine = __instance.StartCoroutine(WaitOneFrame());
        }
    }

    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.GetPlayerCoins))]
    static class StoreGuiGetPlayerCoinsPatch
    {
        static void Postfix(StoreGui __instance, ref int __result)
        {
            __result += GetPlayerCoinsFromCustomData();
        }
    }


    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup))]
    private static class AddItemToInventory
    {
        [HarmonyPriority(Priority.LowerThanNormal)]
        private static bool Prefix(Humanoid __instance, GameObject go, bool autoPickupDelay, bool __runOriginal, ref bool __result)
        {
            if (!__runOriginal || __instance is not Player player || go.GetComponent<ItemDrop>() is not { } itemDrop || player.IsTeleporting() || !itemDrop.CanPickup(autoPickupDelay) || itemDrop.m_nview.GetZDO() is null)
            {
                return true;
            }

            itemDrop.m_itemData.m_dropPrefab ??= ObjectDB.instance.GetItemPrefab(Utils.GetPrefabName(itemDrop.gameObject));
            string itemName = itemDrop.m_itemData.m_shared.m_name;
            int originalAmount = itemDrop.m_itemData.m_stack;

            CheckAutoPickupActive.PickingUp = false;

            if (itemName == CoinToken)
            {
                UpdatePlayerCustomData(GetPlayerCoinsFromCustomData() + originalAmount);
                UpdatePocketUI();
                ZNetScene.instance.Destroy(go);
                player.m_pickupEffects.Create(player.transform.position, Quaternion.identity);
                player.ShowPickupMessage(itemDrop.m_itemData, originalAmount);
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AutoPickup))]
    private static class CheckAutoPickupActive
    {
        public static bool PickingUp = false;
        private static void Prefix() => PickingUp = true;
        private static void Finalizer() => PickingUp = false;
    }


    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), typeof(ItemDrop.ItemData), typeof(int))]
    private static class AutoPickupItemsWithFullInventory
    {
        private static void Postfix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (__result || !CheckAutoPickupActive.PickingUp) return;
            if (item?.m_shared?.m_name == CoinToken)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new Type[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
    public static class Inventory_RemoveItem_Patch
    {
        public static void Postfix(Inventory __instance, string name, int amount, int itemQuality, bool worldLevelBased)
        {
            if (Player.m_localPlayer == null) return;
            if (__instance == Player.m_localPlayer.GetInventory())
            {
                int coinCount = GetPlayerCoinsFromCustomData();
                if (name == CoinToken && GetPlayerCoinsFromCustomData() >= amount)
                {
                    coinCount -= amount;
                    UpdatePlayerCustomData(coinCount);
                    UpdatePocketUI();
                }
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData))]
    static class TransferBetweenInventories
    {
        static bool Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
        {
            if (Player.m_localPlayer == null) return true;
            if (InventoryGui.instance != null && InventoryGui.instance.m_currentContainer != null && fromInventory == InventoryGui.instance.m_currentContainer.GetInventory())
            {
                if (item.m_shared.m_name != CoinToken || __instance != Player.m_localPlayer.GetInventory()) return true;
                GetPlayerCoinsFromCustomData();
                UpdatePlayerCustomData(GetPlayerCoinsFromCustomData() + item.m_stack);
                UpdatePocketUI();
                // if (CouldAdd(__instance, item))
                fromInventory.RemoveItem(item);
                __instance.Changed();
                fromInventory.Changed();
                return false;
            }

            return true;
        }

        public static bool CouldAdd(Inventory inventory, ItemDrop.ItemData item)
        {
            bool flag = true;
            if (item.m_shared.m_maxStackSize > 1)
            {
                for (int index = 0; index < item.m_stack; ++index)
                {
                    ItemDrop.ItemData freeStackItem = inventory.FindFreeStackItem(item.m_shared.m_name, item.m_quality, (float)item.m_worldLevel);
                    if (freeStackItem != null)
                    {
                        ++freeStackItem.m_stack;
                    }
                    else
                    {
                        int num = item.m_stack - index;
                        item.m_stack = num;
                        Vector2i emptySlot = inventory.FindEmptySlot(inventory.TopFirst(item));
                        if (emptySlot.x >= 0)
                        {
                            // Simply do not add the item
                            break;
                        }

                        flag = false;
                        break;
                    }
                }
            }
            else
            {
                Vector2i emptySlot = inventory.FindEmptySlot(inventory.TopFirst(item));
                if (emptySlot.x >= 0)
                {
                    // Simply do not add the item
                }
                else
                    flag = false;
            }

            inventory.Changed();
            return flag;
        }
    }


    internal static void UpdatePocketUI()
    {
        if (InventoryGuiUpdatePatch.pocketUI == null) return;
        // Update the UI with the current coin count
        Transform? coinText = Utils.FindChild(InventoryGuiUpdatePatch.pocketUI.transform, AcText);
        if (coinText == null) return;
        TextMeshProUGUI coinTextTMP = coinText.GetComponent<TextMeshProUGUI>();
        if (coinTextTMP == null) return;
        coinTextTMP.text = $"{GetPlayerCoinsFromCustomData()}";
    }

    private static void CreatePocketUI(InventoryGui instance)
    {
        Transform inv = instance.m_player.transform;
        InventoryGuiUpdatePatch.pocketUI = Object.Instantiate(inv.Find(ArmorName).gameObject, inv);
        InventoryGuiUpdatePatch.pocketUI.name = CoinPocketUIName;
        CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"Creating pocket UI at {InventoryGuiUpdatePatch.pocketUI.GetComponent<RectTransform>().anchoredPosition}");
        if (IsOverlappingUIModInstalled())
        {
            InventoryGuiUpdatePatch.pocketUI.GetComponent<RectTransform>().anchoredPosition += new Vector2(0, -234);
        }
        else
        {
            // Calculate the halfway point between the inv.Find(Armor) and inv.Find(Weight) positions, that's where we want to place the pocket UI
            RectTransform armorRect = inv.Find(ArmorName).GetComponent<RectTransform>();
            RectTransform weightRect = inv.Find(WeightName).GetComponent<RectTransform>();
            InventoryGuiUpdatePatch.pocketUI.GetComponent<RectTransform>().anchoredPosition = new Vector2(armorRect.anchoredPosition.x, (armorRect.anchoredPosition.y + weightRect.anchoredPosition.y) / 2);
        }

        CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"Creating pocket UI at {InventoryGuiUpdatePatch.pocketUI.GetComponent<RectTransform>().anchoredPosition}");
        GameObject? coins = ObjectDB.instance.GetItemPrefab(CoinsPrefabName);
        InventoryGuiUpdatePatch.coinSprite = coins.GetComponent<ItemDrop>().m_itemData.GetIcon();
        InventoryGuiUpdatePatch.pocketUI.transform.Find(ArmorIconName).GetComponent<Image>().sprite = InventoryGuiUpdatePatch.coinSprite;
        InventoryGuiUpdatePatch.pocketUI.transform.SetSiblingIndex(inv.Find(ArmorName).GetSiblingIndex());
        InventoryGuiUpdatePatch.pocketUI.transform.Find(AcText).GetComponent<TextMeshProUGUI>().text = $"{GetPlayerCoinsFromCustomData()}";
        InventoryGuiUpdatePatch.pocketUI.AddComponent<PocketDrop>();
    }

    private static void CreateButton(InventoryGui __instance)
    {
        if (InventoryGuiUpdatePatch.ExtractButton != null)
        {
            return;
        }

        // Clone the take all button and add it to the inventory (InventoryGui.instance.m_takeAllButton)
        InventoryGuiUpdatePatch.ExtractButton = Object.Instantiate(__instance.m_takeAllButton, InventoryGuiUpdatePatch.pocketUI.transform);
        InventoryGuiUpdatePatch.ExtractButton.name = ExtractCoinsButtonName;
        InventoryGuiUpdatePatch.ExtractButton.GetComponentInChildren<TextMeshProUGUI>().text = "\U0001F4E6";

        // Add button to extract coins
        InventoryGuiUpdatePatch.ExtractButton.transform.SetParent(InventoryGuiUpdatePatch.pocketUI.transform, false);

        InventoryGuiUpdatePatch.ExtractButton.onClick = new Button.ButtonClickedEvent();
        InventoryGuiUpdatePatch.ExtractButton.onClick.AddListener(ExtractCoins);

        // Position the button
        RectTransform buttonRectTransform = InventoryGuiUpdatePatch.ExtractButton.GetComponent<RectTransform>();
        if (buttonRectTransform == null)
        {
            // add a rect transform if it doesn't exist
            buttonRectTransform = InventoryGuiUpdatePatch.ExtractButton.gameObject.AddComponent<RectTransform>();
        }

        buttonRectTransform.localPosition = new Vector3(2.5f, -20, 0);
        InventoryGuiUpdatePatch.ExtractButton.transform.localScale = new Vector3(0.4f, 0.4f, 1);
    }

    private static void CreateIcon()
    {
        GameObject? coins = ObjectDB.instance.GetItemPrefab(CoinsPrefabName);
        Sprite? coinIconSprite = coins.GetComponent<ItemDrop>().m_itemData.GetIcon();

        // Add icon to the UI
        GameObject iconObject = new GameObject(CoinIconName);
        iconObject.transform.SetParent(InventoryGuiUpdatePatch.pocketUI.transform, false);

        Image coinIcon = iconObject.AddComponent<Image>();
        coinIcon.sprite = coinIconSprite;
        coinIcon.preserveAspect = true;
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSplitOk))]
static class InventoryGuiOnSplitOkPatch
{
    internal static Inventory throwAwayInventory = null!;
    //internal static int RemoveCount = 0;

    internal static void Prefix(InventoryGui __instance)
    {
        if (__instance.m_splitItem?.m_shared.m_name != CoinToken || !CurrencyPocket.CoinExtractionInProgress) return;
        // Needed because the split inventory sometimes is auto set to the player's inventory. Workaround for now.
        __instance.m_splitInventory = throwAwayInventory;
        UpdatePlayerCustomData(GetPlayerCoinsFromCustomData() - (int)__instance.m_splitSlider.value);
        CurrencyPocket.UpdatePocketUI();
        CurrencyPocket.CoinExtractionInProgress = false;
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
static class PreventSetupDrag
{
    static bool Prefix(InventoryGui __instance)
    {
        if (__instance.m_currentContainer && __instance.m_currentContainer.IsOwner())
        {
            return true;
        }

        return __instance.m_dragInventory is not { m_name: CoinCountCustomData };
    }
}