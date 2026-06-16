using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SlotClass
{
    [SerializeField]private ItemItem item;
    [SerializeField]private int quantity= 0;

    public SlotClass()
    {
        item = null;
        quantity = 0;
    }
    public SlotClass(ItemItem item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }
    public ItemItem GetItem() { return this.item; }
    public int GetQuantity() { return this.quantity; }
    public void AddQuantity(int quanity) { this.quantity = this.quantity + quanity; }
    public void SubQuantity(int quanity) { this.quantity = this.quantity - quanity; }
    public void SetQuantity(int quantity) { this.quantity = quantity; }
    public void AddItem(ItemItem item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    public void RemoveItem()
    {
        this.item = null;
        this.quantity = 0;
    }
}
