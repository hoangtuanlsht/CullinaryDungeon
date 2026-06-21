using PolyAndCode.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEditor.Progress;


public class ShopSlot : MonoBehaviour, ICell, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text quantityText; // THÊM BIẾN NÀY ĐỂ HIỂN THỊ SỐ LƯỢNG

    private ShopController _manager;
    private int _cellIndex;
    private ShopItemData _currentData; // Lưu lại data để dùng khi click mua/bán

    // Hàm này được DataSource (ShopController) gọi và truyền dữ liệu vào
    public void ConfigureCell(ShopController manager, ShopItemData data, int cellIndex)
    {
        _manager = manager;
        _cellIndex = cellIndex;
        _currentData = data;
        // Kiểm tra xem dữ liệu có tồn tại và có gắn file ItemItem không
        if (data != null && data.itemData != null)
        {
            // Lấy trực tiếp ScriptableObject từ data
            ItemItem itemInfo = data.itemData;

            // Tính toán giá tiền trực tiếp tại đây:
            int finalPrice = data.isShopSide ? itemInfo.buyPrice : itemInfo.GetCellPrice();
            priceText.text = finalPrice.ToString();
            if (quantityText != null)
            {
                quantityText.text = data.quantity > 1 ? data.quantity.ToString() : "";
            }
            // Cập nhật Icon (dựa vào biến itemIcon bên trong ItemItem)
            itemIcon.sprite = itemInfo.itemIcon;
            itemIcon.enabled = true;
        }
        else
        {
            priceText.text = "";
            if (quantityText != null) quantityText.text = "";
            itemIcon.sprite = null;
            itemIcon.enabled = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (_manager != null && _currentData != null)
            {
                // Truyền toàn bộ thông tin của ô này về cho Controller xử lý
                _manager.OnItemClicked(_currentData);
            }
        }
    }
}