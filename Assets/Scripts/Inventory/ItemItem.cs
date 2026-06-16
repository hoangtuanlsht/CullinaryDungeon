using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Item",menuName ="New Item")]
public abstract class ItemItem :ScriptableObject
{
    public string itemName { get; set; }

    public Sprite itemIcon;
    public bool isStackable = true;
    public int maxStackQuantity = 0;
    public abstract ItemItem GetItem();
    public abstract CraftingItem GetCraftingItem();
    public abstract ConsumableItem GetConsumableItem();
    public abstract MiscItem GetMiscItem();
}
