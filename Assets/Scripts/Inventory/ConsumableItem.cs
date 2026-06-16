using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Consum", menuName = "Item/Consumable")]
public class ConsumableItem : ItemItem
{
    public float healthRecovery;
    public override ItemItem GetItem()
    {
        return this;
    }
    public override ConsumableItem GetConsumableItem()
    {
        return this;
    }
    public override CraftingItem GetCraftingItem()
    {
        return null;
    }
    public override MiscItem GetMiscItem()
    {
        return null;
    }
}
