using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemClass : MonoBehaviour
{
    public ItemItem itemData; // Bạn kéo file ScriptableObject vật phẩm vào ô này trong Inspector
    public int amount = 1;

    public void Awake()
    {
        itemData = gameObject.GetComponent<ItemItem>();
    }
}
