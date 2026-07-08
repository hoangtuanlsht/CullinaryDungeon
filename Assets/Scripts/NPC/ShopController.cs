using PolyAndCode.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

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
            int price = itemInfo.buyPrice;

            if (Player.coin >= price)
            {
                Player.coin -= price;
                PlayerPrefs.SetInt("coin", Player.coin);
                if (UIManager.instance != null) UIManager.instance.SetCoin(Player.coin);
                _currentShop.RemoveStock(itemInfo, 1);
                InventoryManager.Instance.AddItem(itemInfo, 1);
            }
            else
            {
                Debug.Log("Không đủ tiền mua món đồ này!");
                return;
            }
        }
        else
        {
            int price = itemInfo.GetCellPrice();

            Player.coin += price;
            PlayerPrefs.SetInt("coin", Player.coin);
            if (UIManager.instance != null) UIManager.instance.SetCoin(Player.coin);

            InventoryManager.Instance.RemoveItem(itemInfo, 1);

            _currentShop.AddStock(itemInfo, 1);
        }

        RefreshShopDisplay();
        RefreshPlayerInventoryDisplay();
    }
    private void RefreshShopDisplay()
    {
        shopInventoryList.Clear();
        foreach (var stockItem in _currentShop.GetCurrentStock())
        {
            if (stockItem.quantity > 0) 
            {
                shopInventoryList.Add(new ShopItemData
                {
                    itemData = stockItem.item,
                    quantity = stockItem.quantity,
                    isShopSide = true // Đánh dấu là đồ của Shop
                });
            }
        }

        shopScrollView.ReloadData();
    }

    private void RefreshPlayerInventoryDisplay()
    {
        playerInventoryList.Clear();

        foreach (SlotClass slot in InventoryManager.Instance.items)
        {
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