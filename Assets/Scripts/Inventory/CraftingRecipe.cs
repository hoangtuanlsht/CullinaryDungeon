using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CraftingRecipe
{
    public string recipeName;
    // Mảng nguyên liệu cần thiết (Ví dụ: 4 ô). Nếu ô nào không cần item, để trống (null)
    public ItemItem[] requiredItems = new ItemItem[4];

    // Vật phẩm sinh ra
    public ItemItem resultItem;
    public int resultQuantity = 1;
}
