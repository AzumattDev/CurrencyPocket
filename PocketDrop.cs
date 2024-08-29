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
            if (!InventoryGui.m_instance || !InventoryGui.m_instance.m_dragGo || InventoryGui.m_instance.m_dragItem == null || InventoryGui.m_instance.m_dragItem.m_shared.m_name != CurrencyPocket.CoinToken)
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
            if (InventoryGui.m_instance && InventoryGui.m_instance.m_dragGo && InventoryGui.m_instance.m_dragItem != null && (InventoryGui.m_instance.m_dragItem.m_shared.m_name == CurrencyPocket.CoinToken || InventoryGui.m_instance.m_dragItem.m_shared.m_value > 0) && InventoryGui.m_instance.m_dragInventory != null)
            {
                bool itemIsValuable = InventoryGui.m_instance.m_dragItem.m_shared.m_value > 0 && InventoryGui.m_instance.m_dragItem.m_shared.m_name != CurrencyPocket.CoinToken;
                clicked = true;
                // Add to the pocket
                if (!itemIsValuable)
                {
                    MiscFunctions.UpdatePlayerCustomData(MiscFunctions.GetPlayerCoinsFromCustomData() + InventoryGui.m_instance.m_dragAmount);
                }
                else
                {
                    MiscFunctions.UpdatePlayerCustomData(MiscFunctions.GetPlayerCoinsFromCustomData() + (InventoryGui.m_instance.m_dragAmount * InventoryGui.m_instance.m_dragItem.m_shared.m_value));
                }

                CurrencyPocket.UpdatePocketUI();
                if (InventoryGui.m_instance.m_dragAmount == InventoryGui.m_instance.m_dragItem.m_stack)
                {
                    InventoryGui.m_instance.m_dragInventory.RemoveItem(InventoryGui.m_instance.m_dragItem);
                    PocketDrop.clicked = false;
                }
                else
                {
                    InventoryGui.m_instance.m_dragInventory.RemoveItem(InventoryGui.m_instance.m_dragItem, InventoryGui.m_instance.m_dragAmount);
                    PocketDrop.clicked = false;
                }

                InventoryGui.m_instance.SetupDragItem(null, null, 1);
                InventoryGuiOnSplitOkPatch.throwAwayInventory = null!;
            }
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
}