using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Craft", menuName = "Item/Craft")]

public class CraftingItem : ItemItem
{
    public string itemDescription;
    public override ItemItem GetItem()
    {
        return this;
    }
    public override ConsumableItem GetConsumableItem()
    {
        return null;
    }

    public override CraftingItem GetCraftingItem()
    {
        return this;
    }
    public override MiscItem GetMiscItem()
    {
        return null;
    }
    
}
