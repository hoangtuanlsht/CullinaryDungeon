using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ShopStockItem
{
    public ItemItem item;
    public int quantity;
}

public class ShopNPC : MonoBehaviour // Nếu bạn có Interface IInteractable như video thì thêm vào
{
    public string shopkeeperName = "Merchant";

    [Header("Kho hàng mặc định ban đầu")]
    public List<ShopStockItem> defaultStock = new List<ShopStockItem>();

    // Kho hàng hiện tại (có thể thay đổi khi người chơi mua bớt)
    private List<ShopStockItem> currentStock = new List<ShopStockItem>();

    private void Start()
    {
        // Copy dữ liệu từ kho mặc định sang kho hiện tại khi bắt đầu game
        currentStock = new List<ShopStockItem>();
        foreach (var item in defaultStock)
        {
            currentStock.Add(new ShopStockItem { item = item.item, quantity = item.quantity });
        }
    }

    // Hàm này gọi khi người chơi bấm nút Tương tác với NPC
    public void Interact()
    {
        if (ShopController.instance != null)
        {
            // Mở cửa hàng và truyền chính con NPC này vào Controller
            ShopController.instance.OpenShop(this);
        }
    }
    public void RemoveStock(ItemItem item, int amount)
    {
        foreach (var stock in currentStock)
        {
            if (stock.item == item)
            {
                stock.quantity -= amount;
                // Lưu ý: Ta không cần xóa item khỏi list vì trong ShopController 
                // ta đã có hàm check (quantity > 0) thì mới hiển thị lên UI.
                break;
            }
        }
    }

    public void AddStock(ItemItem item, int amount)
    {
        bool found = false;
        foreach (var stock in currentStock)
        {
            if (stock.item == item)
            {
                stock.quantity += amount;
                found = true;
                break;
            }
        }

        // Nếu cửa hàng chưa từng bán món đồ này trước đây (ví dụ: rác bạn nhặt ngoài đường)
        if (!found)
        {
            currentStock.Add(new ShopStockItem { item = item, quantity = amount });
        }
    }
    public List<ShopStockItem> GetCurrentStock()
    {
        return currentStock;
    }
}
