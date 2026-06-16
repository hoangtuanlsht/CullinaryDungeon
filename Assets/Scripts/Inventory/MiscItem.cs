using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Item/Misc")]
public class MiscItem : ItemItem
{
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
        return null;
    }
    public override MiscItem GetMiscItem()
    {
        return this;
    }
}
