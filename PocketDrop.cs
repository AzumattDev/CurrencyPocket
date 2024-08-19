using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CurrencyPocket
{
    public class PocketDrop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private UITooltip? uiTooltip = null!;
        private GameObject m_tooltipPrefab = null!;
        internal static bool clicked = false;
        internal static Image armorImage = null!;

        private void Awake()
        {
            TryCreateTooltip();
            armorImage = transform.Find("armor_icon").GetComponent<Image>();
        }

        private void Update()
        {
            if (!InventoryGui.m_instance || !InventoryGui.m_instance.m_dragGo || InventoryGui.m_instance.m_dragItem == null || InventoryGui.m_instance.m_dragItem.m_shared.m_name != "$item_coins")
            {
                if (armorImage != null && armorImage.sprite != CurrencyPocket.InventoryGuiUpdatePatch.coinSprite)
                {
                    armorImage.sprite = CurrencyPocket.InventoryGuiUpdatePatch.coinSprite;
                }

                return;
            }

            armorImage.sprite = CurrencyPocketPlugin.DownloadSprite;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            TryCreateTooltip();
            if (!InventoryGui.m_instance || uiTooltip == null || !InventoryGui.m_instance.m_dragGo || InventoryGui.m_instance.m_dragItem == null) return;
            CurrencyPocketPlugin.CurrencyPocketLogger.LogInfo($"GameObject null? {InventoryGui.m_instance.m_dragGo == null} && Item null? {InventoryGui.m_instance.m_dragItem == null}");
            uiTooltip.Set("Coin Drop", "Click here to store coins in your pocket.");
            uiTooltip.OnHoverStart(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (uiTooltip != null)
            {
                UITooltip.HideTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!InventoryGui.m_instance || !InventoryGui.m_instance.m_dragGo || InventoryGui.m_instance.m_dragItem == null || InventoryGui.m_instance.m_dragItem.m_shared.m_name != "$item_coins" || InventoryGui.m_instance.m_dragInventory == null) return;
            clicked = true;
            // Add to the pocket
            MiscFunctions.UpdatePlayerCustomData(MiscFunctions.GetPlayerCoinsFromCustomData() + InventoryGui.m_instance.m_dragAmount);
            CurrencyPocket.UpdatePocketUI();
            if (InventoryGui.m_instance.m_dragAmount == InventoryGui.m_instance.m_dragItem.m_stack)
            {
                CurrencyPocketPlugin.CurrencyPocketLogger.LogInfo($"Removing item from inventory with name: {InventoryGui.m_instance.m_dragItem.m_shared.m_name} and inventory name: {InventoryGui.m_instance.m_dragInventory.m_name}");
                InventoryGui.m_instance.m_dragInventory.RemoveItem(InventoryGui.m_instance.m_dragItem);
                PocketDrop.clicked = false;
            }
            else
            {
                CurrencyPocketPlugin.CurrencyPocketLogger.LogInfo($"Removing {InventoryGui.m_instance.m_dragAmount} coins from inventory with name: {InventoryGui.m_instance.m_dragItem.m_shared.m_name} and inventory name: {InventoryGui.m_instance.m_dragInventory.m_name}");
                InventoryGui.m_instance.m_dragInventory.RemoveItem(InventoryGui.m_instance.m_dragItem, InventoryGui.m_instance.m_dragAmount);
                PocketDrop.clicked = false;
            }

            InventoryGui.m_instance.SetupDragItem(null, null, 1);
            InventoryGuiOnSplitOkPatch.throwAwayInventory = null!;
        }

        private void TryCreateTooltip()
        {
            uiTooltip = GetComponent<UITooltip>();
            if (uiTooltip != null) return;
            if (InventoryGui.instance.m_playerGrid == null) return;
            uiTooltip = InventoryGui.instance.m_playerGrid.m_elements.FirstOrDefault()?.m_tooltip.GetComponent<UITooltip>();
            m_tooltipPrefab = InventoryGui.instance.m_playerGrid.m_elements.FirstOrDefault()?.m_tooltip.m_tooltipPrefab!;
        }
    }

    /*[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateItemDrag))]
    static class InventoryGuiUpdateItemDragPatch
    {
        static void Postfix(InventoryGui __instance, ItemDrop.ItemData ___m_dragItem, Inventory ___m_dragInventory, int ___m_dragAmount)
        {
            if (___m_dragInventory == null || !___m_dragInventory.ContainsItem(___m_dragItem)) return;
            Player? player = Player.m_localPlayer;
            if (___m_dragItem.m_shared.m_name != "$item_coins" || !PocketDrop.clicked) return;
            if (___m_dragAmount <= 0) return;
            if (___m_dragAmount == ___m_dragItem.m_stack)
            {
                CurrencyPocketPlugin.CurrencyPocketLogger.LogInfo($"Removing item from inventory with name: {___m_dragItem.m_shared.m_name} and inventory name: {___m_dragInventory.m_name}");
                ___m_dragInventory.RemoveItem(___m_dragItem);
                PocketDrop.clicked = false;
            }
            else
            {
                CurrencyPocketPlugin.CurrencyPocketLogger.LogInfo($"Removing {___m_dragAmount} coins from inventory with name: {___m_dragItem.m_shared.m_name} and inventory name: {___m_dragInventory.m_name}");
                ___m_dragInventory.RemoveItem(___m_dragItem, ___m_dragAmount);
                PocketDrop.clicked = false;
            }

            if (__instance.m_currentContainer == null && __instance.m_dragInventory.m_name == CurrencyPocket.CoinCountCustomData)
            {
                __instance.SetupDragItem(___m_dragItem, ___m_dragInventory, ___m_dragAmount);
            }
            else
            {
                InventoryGui.m_instance.SetupDragItem(null, null, 1);
                InventoryGui.m_instance.UpdateCraftingPanel();
            }
        }
    }*/
}