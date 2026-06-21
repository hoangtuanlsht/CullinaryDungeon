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
    public int buyPrice;
    [Range(0, 1)]
    public float sellPriceMulti = 0.5f;
    public abstract ItemItem GetItem();
    public abstract CraftingItem GetCraftingItem();
    public abstract ConsumableItem GetConsumableItem();
    public abstract MiscItem GetMiscItem();
    public int GetCellPrice()
    {
        return Mathf.RoundToInt(buyPrice * sellPriceMulti);
    }
}
