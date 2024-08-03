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
            CurrencyPocketPlugin.CurrencyPocketLogger.LogInfo($"GameObject null? {InventoryGui.m_instance.m_dragGo == null} && Item null?{InventoryGui.m_instance.m_dragItem != null}");
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
            if (!InventoryGui.m_instance || !InventoryGui.m_instance.m_dragGo || InventoryGui.m_instance.m_dragItem == null || InventoryGui.m_instance.m_dragItem.m_shared.m_name != "$item_coins") return;
            // Get the item being dragged and the stack size
            clicked = true;
            // Add to the pocket
            MiscFunctions.GetPlayerCoinsFromCustomData();
            MiscFunctions.UpdatePlayerCustomData(MiscFunctions.GetPlayerCoinsFromCustomData() + InventoryGui.m_instance.m_dragItem.m_stack);
            CurrencyPocket.UpdatePocketUI();
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

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateItemDrag))]
    static class InventoryGuiUpdateItemDragPatch
    {
        static void Postfix(InventoryGui __instance, ItemDrop.ItemData ___m_dragItem, Inventory ___m_dragInventory, int ___m_dragAmount)
        {
            if (___m_dragInventory == null || !___m_dragInventory.ContainsItem(___m_dragItem)) return;
            Player? player = Player.m_localPlayer;
            if (player.m_inventory != ___m_dragInventory || ___m_dragItem.m_shared.m_name != "$item_coins" || !PocketDrop.clicked) return;
            if (___m_dragAmount <= 0) return;
            if (___m_dragAmount == ___m_dragItem.m_stack)
            {
                ___m_dragInventory.RemoveItem(___m_dragItem);
                PocketDrop.clicked = false;
            }
            else
            {
                ___m_dragInventory.RemoveItem(___m_dragItem, ___m_dragAmount);
                PocketDrop.clicked = false;
            }

            InventoryGui.m_instance.SetupDragItem(null, null, 0);
            InventoryGui.m_instance.UpdateCraftingPanel();
        }
    }
}