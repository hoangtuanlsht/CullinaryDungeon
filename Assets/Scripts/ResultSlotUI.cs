using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ResultSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text quantityLabel;

    public void UpdateSlotUI()
    {
        // Lấy dữ liệu từ ô kết quả của InventoryManager
        SlotClass slotData = InventoryManager.Instance.resultSlot;

        if (slotData != null && slotData.GetItem() != null)
        {
            itemIcon.sprite = slotData.GetItem().itemIcon;
            itemIcon.enabled = true;
            quantityLabel.text = slotData.GetQuantity() > 1 ? slotData.GetQuantity().ToString() : "";
        }
        else
        {
            itemIcon.sprite = null;
            itemIcon.enabled = false;
            quantityLabel.text = "";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // Gọi hàm logic lấy đồ chế tạo ra (đã viết ở bài trước)
            InventoryManager.Instance.OnClickResultSlot();

            // Cập nhật lại toàn bộ giao diện Crafting
            InventoryManager.Instance.UpdateCraftingUI();
        }
    }
}