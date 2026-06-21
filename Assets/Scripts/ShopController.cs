using PolyAndCode.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEditor.Progress;

[System.Serializable]
public class ShopItemData
{
    public ItemItem itemData;
    public int quantity;          // THÊM DÒNG NÀY: Số lượng vật phẩm hiện có// Kéo thả prefab chứa script Item vào đây
    public bool isShopSide;       // True: Của Shop (Mua) | False: Của Player (Bán)
}

public class ShopController : MonoBehaviour
{
    // ==========================================
    // TẠO SINGLETON
    // ==========================================
    public static ShopController instance { get; private set; }

    [Header("UI References")]
    public GameObject shopPanel;            // Cái Bảng UI tổng (để bật/tắt)
    public TMP_Text shopTitleText;          // Tên cửa hàng (vd: Merchant's Shop)
    public RecyclableScrollRect shopScrollView;
    public RecyclableScrollRect playerScrollView;

    [Header("Data")]
    public List<ShopItemData> shopInventoryList = new List<ShopItemData>();
    public List<ShopItemData> playerInventoryList = new List<ShopItemData>();

    private InventoryDataSource _shopDataSource;
    private InventoryDataSource _playerDataSource;
    private ShopNPC _currentShop;

    private void Awake()
    {
        // Khởi tạo Singleton
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); return; }

        _shopDataSource = new InventoryDataSource(this, shopInventoryList);
        _playerDataSource = new InventoryDataSource(this, playerInventoryList);

        shopScrollView.Initialize(_shopDataSource);
        playerScrollView.Initialize(_playerDataSource);

        // Mặc định tắt UI Shop khi mới vào game
        shopPanel.SetActive(false);
    }

    // ==========================================
    // LOGIC MỞ/TẮT CỬA HÀNG
    // ==========================================

    public void OpenShop(ShopNPC npc)
    {
        _currentShop = npc;
        shopPanel.SetActive(true);
        if (shopTitleText != null) shopTitleText.text = npc.shopkeeperName + "'s Shop";

        RefreshShopDisplay();
        RefreshPlayerInventoryDisplay();
    }

    public void CloseShop()
    {
        _currentShop = null;
        shopPanel.SetActive(false);
    }
    public void OnItemClicked(ShopItemData clickedData)
    {
        ItemItem itemInfo = clickedData.itemData;

        if (clickedData.isShopSide)
        {
            // ==========================================
            // LOGIC MUA HÀNG (TỪ SHOP VÀO TÚI)
            // ==========================================
            int price = itemInfo.buyPrice;

            // 1. Kiểm tra xem người chơi có đủ tiền không
            if (Player.coin >= price)
            {
                // 2. Trừ tiền & Cập nhật UI
                Player.coin -= price;
                PlayerPrefs.SetInt("coin", Player.coin);
                if (UIManager.instance != null) UIManager.instance.SetCoin(Player.coin);

                // 3. Trừ đồ ở cửa hàng
                _currentShop.RemoveStock(itemInfo, 1);

                // 4. Thêm đồ vào túi người chơi
                InventoryManager.Instance.AddItem(itemInfo, 1);
            }
            else
            {
                Debug.Log("Không đủ tiền mua món đồ này!");
                // Tại đây bạn có thể gọi hiện một dòng Text màu đỏ báo lỗi trên UI
                return;
            }
        }
        else
        {
            // ==========================================
            // LOGIC BÁN HÀNG (TỪ TÚI RA SHOP)
            // ==========================================
            int price = itemInfo.GetCellPrice();

            // 1. Cộng tiền cho Player & Cập nhật UI
            Player.coin += price;
            PlayerPrefs.SetInt("coin", Player.coin);
            if (UIManager.instance != null) UIManager.instance.SetCoin(Player.coin);

            // 2. Xóa đồ trong túi người chơi
            InventoryManager.Instance.RemoveItem(itemInfo, 1);

            // 3. Thêm đồ vào cửa hàng (để cửa hàng thu mua và có thể bán lại)
            _currentShop.AddStock(itemInfo, 1);
        }

        // Cuối cùng: Cập nhật lại giao diện cả 2 bên ngay lập tức
        RefreshShopDisplay();
        RefreshPlayerInventoryDisplay();
    }
    // ==========================================
    // NẠP DỮ LIỆU TỰ ĐỘNG
    // ==========================================

    private void RefreshShopDisplay()
    {
        // 1. Xóa sạch List cũ
        shopInventoryList.Clear();

        // 2. Lấy kho hàng từ NPC nạp vào List
        foreach (var stockItem in _currentShop.GetCurrentStock())
        {
            if (stockItem.quantity > 0) // Chỉ hiện đồ nào còn số lượng
            {
                shopInventoryList.Add(new ShopItemData
                {
                    itemData = stockItem.item,
                    quantity = stockItem.quantity,
                    isShopSide = true // Đánh dấu là đồ của Shop
                });
            }
        }

        // 3. Yêu cầu ScrollView render lại (rất tối ưu!)
        shopScrollView.ReloadData();
    }

    private void RefreshPlayerInventoryDisplay()
    {
        playerInventoryList.Clear();

        // Duyệt qua mảng items của InventoryManager
        foreach (SlotClass slot in InventoryManager.Instance.items)
        {
            // Chỉ lấy những ô có chứa vật phẩm và số lượng > 0
            if (slot != null && slot.GetItem() != null && slot.GetQuantity() > 0)
            {
                playerInventoryList.Add(new ShopItemData
                {
                    itemData = slot.GetItem(),
                    quantity = slot.GetQuantity(),
                    isShopSide = false // Báo cho UI biết đây là ô đồ của người chơi
                });
            }
        }

        // Load lại UI
        playerScrollView.ReloadData();
    }
}

public class InventoryDataSource : IRecyclableScrollRectDataSource
{
    private ShopController _controller;
    private List<ShopItemData> _dataList;

    public InventoryDataSource(ShopController controller, List<ShopItemData> dataList)
    {
        _controller = controller;
        _dataList = dataList;
    }
    public int GetItemCount() { return _dataList.Count; }
    public void SetCell(ICell cell, int index)
    {
        ShopSlot slot = (ShopSlot)cell;
        slot.ConfigureCell(_controller, _dataList[index], index);
    }
}