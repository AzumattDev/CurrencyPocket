using System.Collections;
using TMPro;
using UnityEngine.UI;
using static CurrencyPocket.MiscFunctions;
using Object = UnityEngine.Object;

namespace CurrencyPocket;

public class CurrencyPocket
{
    internal static bool CoinExtractionInProgress;

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
                        if (rect)
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
        public static bool PickingUp;
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

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(string), typeof(int), typeof(int), typeof(bool))]
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

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int))]
    static class InventoryMoveItemToThisPatch
    {
        static bool Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item, int amount, int x, int y, ref bool __result)
        {
            Player? player = Player.m_localPlayer;
            if (!player) return true;

            // Only intercept coins moved into the player's main inventory.
            if (item?.m_shared?.m_name != CoinToken || __instance != player.GetInventory())
                return true;

            // If there is a coin stack at target, bounded merge.
            ItemDrop.ItemData targetItem = __instance.GetItemAt(x, y);
            if (targetItem is { m_shared.m_name: CoinToken })
            {
                int maxStack = targetItem.m_shared.m_maxStackSize;
                int available = Math.Max(0, maxStack - targetItem.m_stack);
                int coinsToTransfer = Math.Min(Math.Min(available, amount), item.m_stack);

                if (coinsToTransfer > 0)
                {
                    targetItem.m_stack += coinsToTransfer;
                    item.m_stack -= coinsToTransfer;

                    if (item.m_stack <= 0 && fromInventory != null)
                        fromInventory.RemoveItem(item);

                    __instance.Changed();
                    fromInventory?.Changed();

                    __result = true;
                    return false;
                }

                __result = false;
                return false;
            }

            // No coin stack at target: emulate vanilla bounded add into exact slot.
            int preStack = item.m_stack;
            bool placed = __instance.AddItem(item, amount, x, y);

            int moved = Math.Max(0, preStack - item.m_stack);
            if (moved > 0)
            {
                if (item.m_stack <= 0 && fromInventory != null)
                    fromInventory.RemoveItem(item);

                fromInventory?.Changed();
                __instance.Changed();
            }

            __result = placed;
            return false;
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
        RectTransform? pocketRect = InventoryGuiUpdatePatch.pocketUI.GetComponent<RectTransform>();
        CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"Creating pocket UI at {pocketRect.anchoredPosition}");
        if (IsOverlappingUIModInstalled())
        {
            pocketRect.anchoredPosition += new Vector2(0, -234);
        }
        else
        {
            // Calculate the halfway point between the inv.Find(Armor) and inv.Find(Weight) positions, that's where we want to place the pocket UI
            RectTransform armorRect = inv.Find(ArmorName).GetComponent<RectTransform>();
            RectTransform weightRect = inv.Find(WeightName).GetComponent<RectTransform>();
            InventoryGuiUpdatePatch.pocketUI.transform.SetSiblingIndex(inv.Find(ArmorName).GetSiblingIndex());
            pocketRect.anchoredPosition = new Vector2(armorRect.anchoredPosition.x, (armorRect.anchoredPosition.y + weightRect.anchoredPosition.y) / 2);
        }

        CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"Creating pocket UI at {pocketRect.anchoredPosition}");
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

        var uiGamePad = InventoryGuiUpdatePatch.ExtractButton.GetComponent<UIGamePad>();
        if (uiGamePad.m_hint) Object.Destroy(uiGamePad.m_hint);
        uiGamePad.m_hint = null;
        uiGamePad.m_zinputKey = string.Empty;
        uiGamePad.m_keyCode = KeyCode.None;

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

/*[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
static class InventoryGridDropItemPatch
{
    static void Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, int amount, Vector2i pos)
    {
        //__instance.m_dragInventory is not { m_name: CoinCountCustomData }
        ItemDrop.ItemData itemAt = __instance.m_inventory.GetItemAt(pos.x, pos.y);
        if (itemAt == item)
        {
            CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"Item at {pos.x}, {pos.y} is the same as the item being dropped.");
            return;
        }

        if (itemAt == null || itemAt.m_shared.m_name == item.m_shared.m_name && (item.m_shared.m_maxQuality <= 1 || itemAt.m_quality == item.m_quality) && itemAt.m_shared.m_maxStackSize != 1 || item.m_stack != amount)
        {
            // Make sure to remove from drag inventory what the ItemAt stack was
            if (itemAt != null && itemAt.m_shared.m_name == CoinToken)
            {
                // Check if the itemAt stack can accept coins, if so, add up until the max stack if available, if the itemAt stack is full, do nothing
                int maxStack = itemAt.m_shared.m_maxStackSize;
                CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"{fromInventory.m_name}");
                if (itemAt.m_stack < maxStack && fromInventory.m_name == CoinCountCustomData)
                {
                    int coinsToAdd = Math.Min(item.m_stack, maxStack - itemAt.m_stack);
                    fromInventory.RemoveItem(item, coinsToAdd);
                    fromInventory.Changed();
                    __instance.m_inventory.Changed();
                    CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"Added {coinsToAdd} coins to the item at {pos.x}, {pos.y}");
                }
            }

            CurrencyPocketPlugin.CurrencyPocketLogger.LogDebug($"Item at {pos.x}, {pos.y} is not the same as the item being dropped., first block reached");
            return;
        }
    }
}*/