using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static CurrencyPocket.CurrencyPocketPlugin;

namespace CurrencyPocket;

public class PocketDrop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private UITooltip? uiTooltip;
    private GameObject m_tooltipPrefab = null!;
    internal static bool clicked;
    internal static Image armorImage = null!;

    private void Awake()
    {
        TryCreateTooltip();
        armorImage = transform.Find(ArmorIconName).GetComponent<Image>();
    }

    private void Update()
    {
        if (!InventoryGui.m_instance || !InventoryGui.m_instance.m_dragGo || InventoryGui.m_instance.m_dragItem == null || InventoryGui.m_instance.m_dragItem.m_shared.m_name != CoinToken)
        {
            if (armorImage != null && armorImage.sprite != CurrencyPocket.InventoryGuiUpdatePatch.coinSprite)
            {
                armorImage.sprite = CurrencyPocket.InventoryGuiUpdatePatch.coinSprite;
            }

            return;
        }

        armorImage.sprite = DownloadSprite;
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
        if (!InventoryGui.m_instance) return;
        switch (eventData.button)
        {
            case PointerEventData.InputButton.Left when InventoryGui.m_instance.m_dragGo && InventoryGui.m_instance.m_dragItem != null && InventoryGui.m_instance.m_dragInventory != null:
            {
                ItemDrop.ItemData? dragItem = InventoryGui.m_instance.m_dragItem;
                bool isCoin = dragItem.m_shared.m_name == CoinToken;
                bool itemIsValuable = dragItem.m_shared.m_value > 0 && !isCoin;

                // If it's not a coin and not valuable, reject
                if (!isCoin && !itemIsValuable) return;

                // If it's valuable, check config settings
                if (itemIsValuable)
                {
                    // Check if valuable items are allowed
                    if (!AllowValuableItems.Value) return;

                    // Check if prefab allowlist is configured and if so, verify the item is in it
                    string allowedPrefabs = AllowedValuablePrefabs.Value;
                    if (!string.IsNullOrWhiteSpace(allowedPrefabs))
                    {
                        string prefabName = dragItem.m_dropPrefab?.name ?? "";
                        IEnumerable<string> allowedList = allowedPrefabs.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p));
                        if (!allowedList.Contains(prefabName, StringComparer.OrdinalIgnoreCase)) return;
                    }
                }

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
                }
                else
                {
                    InventoryGui.m_instance.m_dragInventory.RemoveItem(InventoryGui.m_instance.m_dragItem, InventoryGui.m_instance.m_dragAmount);
                }

                clicked = false;

                InventoryGui.m_instance.SetupDragItem(null, null, 1);
                InventoryGuiOnSplitOkPatch.throwAwayInventory = null!;
                break;
            }
            case PointerEventData.InputButton.Right:
            {
                CurrencyPocketLogger.LogWarning("Right clicked");
            }
                break;
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