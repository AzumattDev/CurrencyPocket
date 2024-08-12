using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurrencyPocket;

public class CurrencyPocket
{
    internal const string CoinCountCustomData = "CoinPocket_CoinCount";
    internal static bool coinExtractionInProgress = false;

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    public static class InventoryGuiUpdatePatch
    {
        public static Button ExtractButton = null!;
        public static GameObject pocketUI = null!;
        public static Sprite coinSprite = null!;

        [HarmonyAfter("org.bepinex.plugins.jewelcrafting")]
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
                    if (child.name is "Armor" or "Weight" or "Jewelcrafting Synergy" or "CoinPocketUI" or "Trash")
                    {
                        RectTransform? rect = child.gameObject.GetComponent<RectTransform>();
                        if (rect != null)
                        {
                            rect.anchoredPosition += new Vector2(0, 45);
                        }
                    }

                    if (Chainloader.PluginInfos.ContainsKey("randyknapp.mods.equipmentandquickslots") || Chainloader.PluginInfos.ContainsKey("Azumatt.AzuExtendedPlayerInventory"))
                    {
                        if (child.name is "sortInventoryButton" or "restockAreaButton" or "quickStackAreaButton" or "favoritingTogglingButton")
                        {
                            RectTransform? rect = child.gameObject.GetComponent<RectTransform>();
                            if (rect != null)
                            {
                                if (child.name == "favoritingTogglingButton")
                                    rect.anchoredPosition += new Vector2(0, 20);
                                else if (!Chainloader.PluginInfos.ContainsKey("randyknapp.mods.equipmentandquickslots") && Chainloader.PluginInfos.ContainsKey("Azumatt.AzuExtendedPlayerInventory"))
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
            __result += MiscFunctions.GetPlayerCoinsFromCustomData();
        }
    }

    /*[HarmonyPatch(typeof(Player), nameof(Player.UpdateKnownRecipesList))]
    static class UpdateKnownRecipesListPatch
    {
        internal static bool skip;

        static void Prefix()
        {
            skip = true;
        }

        static void Postfix()
        {
            skip = false;
        }
    }*/

    /*[HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    static class ConsumeResourcesPatch
    {
        static bool Prefix(Player __instance, Piece.Requirement[] requirements, int qualityLevel, int itemQuality = -1)
        {
            MiscFunctions.ProcessRequirements(requirements, qualityLevel, __instance, itemQuality);
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirementList))]
    static class InventoryGuiCollectRequirements
    {
        public static Dictionary<Piece.Requirement, int> actualAmounts = new();

        private static void Prefix()
        {
            actualAmounts.Clear();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    static class InventoryGuiSetupRequirementPatch
    {
        static void Postfix(InventoryGui __instance, Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality)
        {
            if (req == null || req?.m_resItem == null || req.m_resItem.m_itemData?.m_shared == null)
            {
                return;
            }

            req.m_resItem.m_itemData.m_dropPrefab = req.m_resItem.gameObject;
            if (req.m_resItem.m_itemData.m_dropPrefab == null)
            {
                return;
            }

            int invAmount = player.GetInventory().CountItems(req.m_resItem.m_itemData.m_shared.m_name);
            TextMeshProUGUI text = elementRoot.transform.Find("res_amount").GetComponent<TextMeshProUGUI>();
            if (text == null) return;
            if (!int.TryParse(text.text, out int amount))
            {
                amount = req.GetAmount(quality);
            }

            if (amount <= 0)
            {
                return;
            }

            if (invAmount < amount)
            {
                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(req.m_resItem.GetPrefabName(req.m_resItem.gameObject.name));
                if (itemPrefab == null)
                {
                    return;
                }

                req.m_resItem.m_itemData.m_dropPrefab = itemPrefab;

                try
                {
                    if (req.m_resItem.m_itemData.m_dropPrefab == null)
                        return;
                    string sharedName = req.m_resItem.m_itemData.m_shared.m_name;
                    if (sharedName == "$item_coins")
                        invAmount += MiscFunctions.GetPlayerCoinsFromCustomData();
                }
                catch (System.Exception e)
                {
                    // ignored
                }


                if (invAmount >= amount)
                {
                    text.color = ((Mathf.Sin(Time.time * 10f) > 0f)
                        ? Color.yellow
                        : Color.white);
                    InventoryGuiCollectRequirements.actualAmounts[req] = amount;
                }
            }

            text.text = string.Format("{0}/{1}", invAmount, amount);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirementItems), new[] { typeof(Recipe), typeof(bool), typeof(int) })]
    static class PlayerHaveRequirementsPatch
    {
        static void Postfix(Player __instance, ref bool __result, Recipe piece, bool discover, int qualityLevel, HashSet<string> ___m_knownMaterial)
        {
            if (__result || discover)
                return;
            bool cando = false;
            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (!requirement.m_resItem) continue;
                bool proceed = MiscFunctions.CheckItemDropIntegrity(requirement.m_resItem);
                if (!proceed)
                    continue;
                if (!InventoryGuiCollectRequirements.actualAmounts.TryGetValue(requirement, out int amount))
                {
                    amount = requirement.GetAmount(qualityLevel);
                }

                int invAmount = __instance.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name);
                if (invAmount >= amount) continue;

                GameObject itemPrefab = MiscFunctions.GetItemPrefabFromGameObject(requirement.m_resItem, requirement.m_resItem.gameObject)!;
                requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                if (itemPrefab == null)
                    continue;
                if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                {
                    continue;
                }

                string itemPrefabName = Utils.GetPrefabName(requirement.m_resItem.name);
                string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;

                if (requirement.m_resItem?.m_itemData?.m_dropPrefab == null)
                    continue;
                if (sharedName == "$item_coins")
                    invAmount += MiscFunctions.GetPlayerCoinsFromCustomData();
                ;


                if (piece.m_requireOnlyOneIngredient)
                {
                    if (invAmount < amount) continue;
                    //cando = true;
                    if (__instance.m_knownMaterial.Contains(requirement.m_resItem.m_itemData.m_shared.m_name))
                    {
                        cando = true;
                    }
                }
                else if (invAmount < amount)
                    return;
                else
                {
                    cando = true;
                }
            }

            if (cando)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
    static class HaveRequirementsPatch2
    {
        [HarmonyWrapSafe]
        static void Postfix(Player __instance, ref bool __result, Piece piece, Player.RequirementMode mode, HashSet<string> ___m_knownMaterial, Dictionary<string, int> ___m_knownStations)
        {
            try
            {
                if (__result || __instance?.transform?.position == null)
                    return;
                if (piece == null)
                    return;

                if (piece.m_craftingStation)
                {
                    if (mode is Player.RequirementMode.IsKnown or Player.RequirementMode.CanAlmostBuild)
                    {
                        if (!___m_knownStations.ContainsKey(piece.m_craftingStation.m_name))
                        {
                            return;
                        }
                    }
                    else if (!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, __instance.transform.position))
                    {
                        return;
                    }
                }

                if (piece.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(piece.m_dlc))
                {
                    return;
                }

                foreach (Piece.Requirement requirement in piece.m_resources)
                {
                    if (requirement.m_resItem == null)
                        continue;
                    if (requirement.m_resItem && requirement.m_amount > 0)
                    {
                        if (!MiscFunctions.CheckItemDropIntegrity(requirement.m_resItem))
                            continue;
                        requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                        if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                            continue;
                        switch (mode)
                        {
                            case Player.RequirementMode.IsKnown
                                when !___m_knownMaterial.Contains(requirement.m_resItem.m_itemData.m_shared.m_name):
                                return;
                            case Player.RequirementMode.CanAlmostBuild when __instance.GetInventory().HaveItem(requirement.m_resItem.m_itemData.m_shared.m_name):
                                continue;
                            case Player.RequirementMode.CanAlmostBuild:
                            {
                                bool hasItem = false;
                                string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;

                                requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                                if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                                    continue;

                                if (MiscFunctions.GetPlayerCoinsFromCustomData() > 0)
                                {
                                    hasItem = true;
                                    break;
                                }


                                if (!hasItem)
                                    return;
                                break;
                            }
                            case Player.RequirementMode.CanBuild when __instance.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name) < requirement.m_amount:
                            {
                                int hasItems = __instance.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name);
                                requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                                if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                                    continue;
                                string itemPrefabName = requirement.m_resItem.name;
                                string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;
                                if (sharedName == "$item_coins")
                                    hasItems += MiscFunctions.GetPlayerCoinsFromCustomData();
                                if (hasItems >= requirement.m_amount)
                                {
                                    break;
                                }

                                if (hasItems < requirement.m_amount)
                                    return;
                                break;
                            }
                        }
                    }
                }

                __result = true;
            }
            catch
            {
            }
        }
    }*/


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

            if (itemName == "$item_coins")
            {
                MiscFunctions.UpdatePlayerCustomData(MiscFunctions.GetPlayerCoinsFromCustomData() + originalAmount);
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

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new System.Type[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
    public static class Inventory_RemoveItem_Patch
    {
        public static void Postfix(Inventory __instance, string name, int amount, int itemQuality, bool worldLevelBased)
        {
            if (__instance == Player.m_localPlayer.GetInventory())
            {
                int coinCount = MiscFunctions.GetPlayerCoinsFromCustomData();
                if (name == "$item_coins" && MiscFunctions.GetPlayerCoinsFromCustomData() >= amount)
                {
                    coinCount -= amount;
                    MiscFunctions.UpdatePlayerCustomData(coinCount);
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
                if (item.m_shared.m_name != "$item_coins" || __instance != Player.m_localPlayer.GetInventory()) return true;
                MiscFunctions.GetPlayerCoinsFromCustomData();
                MiscFunctions.UpdatePlayerCustomData(MiscFunctions.GetPlayerCoinsFromCustomData() + item.m_stack);
                CurrencyPocket.UpdatePocketUI();
                if (CouldAdd(__instance, item))
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
        Transform? coinText = Utils.FindChild(InventoryGuiUpdatePatch.pocketUI.transform, "ac_text");
        if (coinText == null) return;
        TextMeshProUGUI coinTextTMP = coinText.GetComponent<TextMeshProUGUI>();
        if (coinTextTMP == null) return;
        coinTextTMP.text = $"{MiscFunctions.GetPlayerCoinsFromCustomData()}";
    }

    private static void CreatePocketUI(InventoryGui instance)
    {
        Transform inv = instance.m_player.transform;
        InventoryGuiUpdatePatch.pocketUI = Object.Instantiate(inv.Find("Armor").gameObject, inv);
        InventoryGuiUpdatePatch.pocketUI.name = "CoinPocketUI";
        CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"Creating pocket UI at {InventoryGuiUpdatePatch.pocketUI.GetComponent<RectTransform>().anchoredPosition}");
        InventoryGuiUpdatePatch.pocketUI.GetComponent<RectTransform>().anchoredPosition += new Vector2(0, -234);
        CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"Creating pocket UI at {InventoryGuiUpdatePatch.pocketUI.GetComponent<RectTransform>().anchoredPosition}");
        GameObject? coins = ObjectDB.instance.GetItemPrefab("Coins");
        InventoryGuiUpdatePatch.coinSprite = coins.GetComponent<ItemDrop>().m_itemData.GetIcon();
        InventoryGuiUpdatePatch.pocketUI.transform.Find("armor_icon").GetComponent<Image>().sprite = InventoryGuiUpdatePatch.coinSprite;
        InventoryGuiUpdatePatch.pocketUI.transform.SetSiblingIndex(inv.Find("Armor").GetSiblingIndex());
        InventoryGuiUpdatePatch.pocketUI.transform.Find("ac_text").GetComponent<TextMeshProUGUI>().text = $"{MiscFunctions.GetPlayerCoinsFromCustomData()}";
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
        InventoryGuiUpdatePatch.ExtractButton.name = "ExtractCoinsButton";
        InventoryGuiUpdatePatch.ExtractButton.GetComponentInChildren<TextMeshProUGUI>().text = "\U0001F4E6";

        // Add button to extract coins
        InventoryGuiUpdatePatch.ExtractButton.transform.SetParent(InventoryGuiUpdatePatch.pocketUI.transform, false);

        InventoryGuiUpdatePatch.ExtractButton.onClick = new Button.ButtonClickedEvent();
        InventoryGuiUpdatePatch.ExtractButton.onClick.AddListener(MiscFunctions.ExtractCoins);

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
        GameObject? coins = ObjectDB.instance.GetItemPrefab("Coins");
        Sprite? coinIconSprite = coins.GetComponent<ItemDrop>().m_itemData.GetIcon();

        // Add icon to the UI
        GameObject iconObject = new GameObject("CoinIcon");
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

    static void Prefix(InventoryGui __instance)
    {
        if (__instance.m_splitItem?.m_shared.m_name != "$item_coins" || !CurrencyPocket.coinExtractionInProgress) return;
        // Needed because the split inventory sometimes is auto set to the player's inventory. Workaround for now.
        __instance.m_splitInventory = throwAwayInventory;

        Player? player = Player.m_localPlayer;
        if (player.GetInventory().CanAddItem(__instance.m_splitItem, (int)__instance.m_splitSlider.value))
        {
            CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"{__instance.m_splitItem} {__instance.m_splitInventory} {(int)__instance.m_splitSlider.value}");
            if (__instance.m_currentContainer == null)
            {
                player.GetInventory().AddItem(__instance.m_splitItem.m_dropPrefab, (int)__instance.m_splitSlider.value);
                __instance.m_splitInventory.RemoveItem(__instance.m_splitItem, (int)__instance.m_splitSlider.value);
                __instance.SetupDragItem(__instance.m_splitItem, __instance.m_splitInventory, (int)__instance.m_splitSlider.value);
            }


            MiscFunctions.UpdatePlayerCustomData(MiscFunctions.GetPlayerCoinsFromCustomData() - (int)__instance.m_splitSlider.value);
            CurrencyPocket.UpdatePocketUI();
            CurrencyPocket.coinExtractionInProgress = false;
            throwAwayInventory = null!;
        }
        else
        {
            player.Message(MessageHud.MessageType.Center, "$inventory_full");
            CurrencyPocket.coinExtractionInProgress = false;
        }
    }
}