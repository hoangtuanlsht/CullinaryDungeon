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

    //[SerializeField] private List<SlotClass> items = new List<SlotClass>();
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
        //scrollRect.DataSource = this;
        if (scrollRect != null)
        {
            scrollRect.Initialize(this); // Gọi hàm này để thiết lập DataSource và đúc Cell
        }
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
            inventory.GetComponent<RectTransform>().anchoredPosition = posInventory.y == 1000 ? new Vector3(600,0,0) : new Vector3(600,1000,0);
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
            Debug.Log("CCs");
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



}
