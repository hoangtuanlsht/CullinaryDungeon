using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "New Item")]

public abstract class ItemItem :ScriptableObject
{
    [SerializeField] private string itemName;
    public string ItemName => itemName;
    [Tooltip("Id ổn định để lưu game. Đặt một lần không đổi sau khi đã có save")]
    [SerializeField] private string itemId;
    public string ItemId => itemId;
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
#if UNITY_EDITOR
    internal void EditorSetId(string id) => itemId = id;
#endif
}
