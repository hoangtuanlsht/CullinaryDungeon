using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PolyAndCode.UI;

public class InventoryManager : MonoBehaviour,IRecyclableScrollRectDataSource
{
    [Header("UI References")]
    [SerializeField] private RecyclableScrollRect scrollRect;
    public Image itemCursor;
    public GameObject inventory;

    [Header("Data")]
    [SerializeField]private int totalSlots = 30;
    [SerializeField] public SlotClass[] items;
    [SerializeField] private SlotClass[] startingItems;

    [Header("Moving/Dragging")]
    [SerializeField] private SlotClass movingSlot = new SlotClass();
    private SlotClass originalSlot;
    private SlotClass tempSlot = new SlotClass();
    public bool isMoving;

    private static InventoryManager instance;
    public static InventoryManager Instance { get { return instance; } }

    [Header("Crafting System")]
    public List<CraftingRecipe> recipes;
    public SlotClass[] craftingSlots = new SlotClass[4];
    public SlotClass resultSlot = new SlotClass();

    [Header("Crafting UI Link")]
    public CraftingSlotUI[] craftingSlotUIs = new CraftingSlotUI[4];
    public ResultSlotUI resultSlotUI;
    //[SerializeField] private List<SlotClass> items = new List<SlotClass>();
    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        items = new SlotClass[totalSlots];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = new SlotClass();
        }

        for (int i = 0; i < startingItems.Length; i++)
        {
            items[i] = new SlotClass();
        }
        for (int i = 0; i < startingItems.Length; i++)
        {
            if (startingItems[i].GetQuantity() >= 1)
            {
                items[i] = startingItems[i];
            }

        }
        craftingSlots = new SlotClass[4];
        for (int i = 0; i < craftingSlots.Length; i++)
        {
            // Tạo mới một instance SlotClass cho từng ô để ô đó sẵn sàng chứa item
            craftingSlots[i] = new SlotClass();
        }

        // Khởi tạo instance cho ô chứa kết quả chế tạo
        resultSlot = new SlotClass();
        if (scrollRect != null)
        {
            scrollRect.Initialize(this); // Gọi hàm này để thiết lập DataSource và đúc Cell
        }
        inventory.GetComponent<RectTransform>().anchoredPosition = new Vector3 (600,1000,0); 
    }
    
    private void Update()
    {       
        if (isMoving && movingSlot.GetItem()!= null)
        {
            itemCursor.enabled = true;
            itemCursor.transform.position = Input.mousePosition;
            itemCursor.sprite = movingSlot.GetItem().itemIcon;
        }
        else
        {
            itemCursor.enabled = false;
            itemCursor.sprite = null;
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            Vector3 posInventory = inventory.GetComponent<RectTransform>().anchoredPosition;
            inventory.GetComponent<RectTransform>().anchoredPosition = posInventory.y == 1000 ? new Vector3(500,0,0) : new Vector3(600,1000,0);
        }
    }


    public int GetItemCount()
    {
        return items.Length;
    }

    public void SetCell(ICell cell, int index)
    {
        //Casting to the implemented Cell
        var item = cell as CellItemData;
        item.ConfigureCell(this,items[index], index);
    }
    // Update is called once per frame
    public void AddItem(ItemItem item, int quantity)
    {
        SlotClass slot = ContainItem(item);
        if (slot != null)
        {
            slot.AddQuantity(quantity);
        }
        else
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].GetItem() == null)
                {
                    items[i].AddItem(item, quantity);
                    break;
                }
            }

        }
        scrollRect.ReloadData();
    }
    public void RemoveItem(ItemItem item, int quantity)
    {
        SlotClass temp = ContainItem(item);
        if (temp != null)
        {
            if (temp.GetQuantity() > 1)
            {
                temp.SubQuantity(quantity);
            }
            else
            {
                int slotToRemoveIndex = 0;
                SlotClass slotToRemove = new SlotClass();
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].GetItem() == item)
                    {
                        slotToRemoveIndex = i;
                        break;
                    }
                }

                items[slotToRemoveIndex].RemoveItem();
            }
        }
        else
        {
            return;
        }
        scrollRect.ReloadData();
    }
    public SlotClass ContainItem(ItemItem item)
    {
        foreach(SlotClass slot in items)
        {
            if(slot != null && slot.GetItem() == item)
            {
                return slot;
            }
        }return null;
    }
    public void OnLeftClickSlot(int slotIndex)
    {
        if (isMoving)
        {
            EndMove(slotIndex);
        }
        else
        {
            BeginMove(slotIndex);
        }
    }
    public void OnRightClickSlot(int slotIndex)
    {
        if (!isMoving)
        {
            BeginSplit(slotIndex);
        }
    }

    private void BeginMove(int index)
    {
        originalSlot = items[index];
        if (originalSlot == null || originalSlot.GetItem() == null)
        {
            return;
        }
        movingSlot.AddItem(originalSlot.GetItem(), originalSlot.GetQuantity());

        originalSlot.RemoveItem();
        isMoving = true;
        scrollRect.ReloadData();
    }
    private void BeginSplit(int index)
    {
        originalSlot = items[index];
        
        if (originalSlot == null || originalSlot.GetItem() == null) return;
        
        if(originalSlot.GetQuantity() < 1)
        {
            return ;
        }
        movingSlot.AddItem(originalSlot.GetItem(), Mathf.CeilToInt(originalSlot.GetQuantity()/2f));

        originalSlot.SubQuantity(Mathf.CeilToInt(originalSlot.GetQuantity() / 2f));
        isMoving = true;
        scrollRect.ReloadData();
        return;

    }
    private void EndMove(int index)
    {
        originalSlot = items[index];
        Debug.Log("CCs");
        // 1. NẾU Ô ĐÍCH ĐANG TRỐNG -> Đặt đồ xuống
        if (originalSlot == null)
        {
            AddItem(movingSlot.GetItem(), movingSlot.GetQuantity());
        }
        // 2. NẾU Ô ĐÍCH ĐÃ CÓ ĐỒ
        else
        {
            if (originalSlot.GetItem() != null)
            {
                if (originalSlot.GetItem() == movingSlot.GetItem())
                {
                    if (originalSlot.GetItem().isStackable)
                    {
                        Debug.Log("CC");
                        int itemMaxStack = originalSlot.GetItem().maxStackQuantity;
                        int count = originalSlot.GetQuantity() + movingSlot.GetQuantity();
                        if (count > itemMaxStack) {
                            int remain = count - itemMaxStack;
                            originalSlot.SetQuantity(itemMaxStack);
                            movingSlot.SetQuantity(remain);
                            isMoving = true;
                            scrollRect.ReloadData();
                            return;
                        }
                        originalSlot.AddQuantity(movingSlot.GetQuantity());
                        movingSlot.RemoveItem();
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    tempSlot.AddItem(originalSlot.GetItem(), originalSlot.GetQuantity());
                    originalSlot.AddItem(movingSlot.GetItem(), movingSlot.GetQuantity());
                    movingSlot.AddItem(tempSlot.GetItem(), tempSlot.GetQuantity());
                    tempSlot.RemoveItem();

                    scrollRect.ReloadData();
                    return;
                }
            }
            else
            {
                originalSlot.AddItem(movingSlot.GetItem(), movingSlot.GetQuantity());
                movingSlot.RemoveItem();
            }
        }
        isMoving = false;
        scrollRect.ReloadData();
        return;
    }
    public void CheckForCompletedRecipes()
    {
        resultSlot.RemoveItem(); // Xóa kết quả cũ

        foreach (CraftingRecipe recipe in recipes)
        {
            // 1. Gom các item ĐANG CÓ trên bàn chế tạo (bỏ qua các ô trống)
            List<ItemItem> itemsOnTable = new List<ItemItem>();
            for (int i = 0; i < craftingSlots.Length; i++)
            {
                if (craftingSlots[i].GetItem() != null)
                {
                    itemsOnTable.Add(craftingSlots[i].GetItem());
                }
            }

            // 2. Gom các item YÊU CẦU của công thức này (bỏ qua phần tử null)
            List<ItemItem> requiredItems = new List<ItemItem>();
            foreach (ItemItem item in recipe.requiredItems)
            {
                if (item != null)
                {
                    requiredItems.Add(item);
                }
            }

            // 3. Nếu tổng số món đồ trên bàn khác tổng số lượng yêu cầu -> Loại công thức này
            if (itemsOnTable.Count != requiredItems.Count)
            {
                continue;
            }

            // 4. Kiểm tra chéo (Bất chấp vị trí)
            bool isMatch = true;
            foreach (ItemItem reqItem in requiredItems)
            {
                // Nếu tìm thấy nguyên liệu yêu cầu nằm trên bàn
                if (itemsOnTable.Contains(reqItem))
                {
                    // Gạch bỏ nguyên liệu đó khỏi danh sách tạm để không bị đếm trùng lặp
                    // (Đề phòng trường hợp công thức cần 2 Gỗ, mà trên bàn chỉ có 1 Gỗ)
                    itemsOnTable.Remove(reqItem);
                }
                else
                {
                    // Thiếu nguyên liệu -> Sai công thức
                    isMatch = false;
                    break;
                }
            }

            // 5. Nếu vượt qua mọi bài kiểm tra -> Khớp!
            if (isMatch)
            {
                resultSlot.AddItem(recipe.resultItem, recipe.resultQuantity);
                Debug.Log("Đã tìm thấy công thức: " + recipe.recipeName);
                break; // Tìm thấy thì dừng tìm các công thức khác
            }
        }
    }
    public void OnCraftingSlotLeftClick(int craftSlotIndex)
    {
        SlotClass targetCraftSlot = craftingSlots[craftSlotIndex];

        if (isMoving) // TRƯỜNG HỢP 1: Bạn đang cầm đồ trên chuột
        {
            // Nếu ô Crafting trống -> Bỏ 1 item vào
            if (targetCraftSlot.GetItem() == null)
            {
                targetCraftSlot.AddItem(movingSlot.GetItem(), 1);
                movingSlot.SubQuantity(1);
            }
            // Nếu ô Crafting đã có đồ CÙNG LOẠI -> Thêm 1 item vào (Cộng dồn)
            else if (targetCraftSlot.GetItem() == movingSlot.GetItem())
            {
                targetCraftSlot.AddQuantity(1);
                movingSlot.SubQuantity(1);
            }

            // Nếu trên tay hết đồ thì tắt trạng thái cầm đồ
            if (movingSlot.GetQuantity() <= 0)
            {
                isMoving = false;
                movingSlot.RemoveItem();
            }
        }
        else // TRƯỜNG HỢP 2: Tay bạn đang trống (Không cầm đồ)
        {
            // Click vào ô Crafting có đồ -> Cầm đồ đó lên lại
            if (targetCraftSlot.GetItem() != null)
            {
                movingSlot.AddItem(targetCraftSlot.GetItem(), targetCraftSlot.GetQuantity());
                isMoving = true;
                targetCraftSlot.RemoveItem(); // Xóa đồ ở ô Crafting đi
            }
        }

        // Cập nhật lại công thức và hình ảnh UI
        CheckForCompletedRecipes();
        UpdateCraftingUI();
    }
    public void OnClickResultSlot()
    {
        if (resultSlot.GetItem() != null && !isMoving)
        {
            // Cầm vật phẩm kết quả lên chuột (movingSlot)
            movingSlot.AddItem(resultSlot.GetItem(), resultSlot.GetQuantity());
            isMoving = true;
            resultSlot.RemoveItem();

            // Trừ nguyên liệu ở 4 ô chế tạo
            for (int i = 0; i < craftingSlots.Length; i++)
            {
                if (craftingSlots[i].GetItem() != null)
                {
                    craftingSlots[i].SubQuantity(1);

                    // SỬA LỖI Ở ĐÂY: Nếu số lượng về 0 thì phải xóa sạch đồ khỏi ô
                    if (craftingSlots[i].GetQuantity() <= 0)
                    {
                        craftingSlots[i].RemoveItem();
                    }
                }
            }

            // Cập nhật lại công thức và hình ảnh
            CheckForCompletedRecipes();
            UpdateCraftingUI();
        }
    }
    public void UpdateCraftingUI()
    {
        for (int i = 0; i < craftingSlotUIs.Length; i++)
        {
            if (craftingSlotUIs[i] != null) craftingSlotUIs[i].UpdateSlotUI();
        }
        if (resultSlotUI != null) resultSlotUI.UpdateSlotUI();
        if (scrollRect != null) scrollRect.ReloadData();
    }
}
