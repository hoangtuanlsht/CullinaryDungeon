using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CraftingSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Cấu hình")]
    public int slotIndex; // Gán ID từ 0 đến 3 cho 4 ô

    [Header("UI References")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text quantityLabel;

    // Hàm này dùng để vẽ lại giao diện của ô dựa trên dữ liệu từ InventoryManager
    public void UpdateSlotUI()
    {
        // Lấy dữ liệu SlotClass tương ứng từ InventoryManager
        SlotClass slotData = InventoryManager.Instance.craftingSlots[slotIndex];
        
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

    // Khi người chơi click chuột vào ô này
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // Gọi hàm logic thả item vào ô chế tạo (đã viết ở bài trước)
            InventoryManager.Instance.OnCraftingSlotLeftClick(slotIndex);

            // Cập nhật lại toàn bộ giao diện Crafting sau khi click
            InventoryManager.Instance.UpdateCraftingUI();
        }
    }
}