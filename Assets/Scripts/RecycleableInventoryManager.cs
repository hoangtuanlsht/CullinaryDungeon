using PolyAndCode.UI;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static Unity.VisualScripting.Member;
using static UnityEditor.PlayerSettings;

public class RecycleableInventoryManager : MonoBehaviour, IRecyclableScrollRectDataSource
{
    
    [SerializeField]RecyclableScrollRect _recyclableScrollRect;
    [SerializeField]
    private int _dataLength;
    //Dummy data List
    private List<InventoryItems> invenItems = new List<InventoryItems>();
    //Recyclable scroll rect's data source must be assigned in Awake.
    private void Awake()
    {
        _recyclableScrollRect.DataSource = this;
    }
    public void Start()
    {
        List<InventoryItems> listItem = new List<InventoryItems>();
        for(int i = 0; i < 50; i++)
        {
            InventoryItems invenItem = new InventoryItems();
            invenItem.name = "Name_" + i.ToString();
            invenItem.description = "Des" +i.ToString();
            listItem.Add(invenItem);

        }
        SetListItem(listItem);
        _recyclableScrollRect.ReloadData();
    }
    public void Update()
    {
        if (Input.GetKeyUp(KeyCode.K))
        {
            InventoryItems inventoryItem = new InventoryItems("Ca","CC");
            invenItems.Add(inventoryItem);
            _recyclableScrollRect.ReloadData();
        }
    }
    public void SetListItem(List<InventoryItems> list)
    {
        invenItems = list;
    }
    public int GetItemCount()
    {
        return invenItems.Count;
    }
   
    public void SetCell(ICell cell, int index)
    {
        //Casting to the implemented Cell
        var item = cell as CellItemData;
        item.ConfigureCell(invenItems[index], index);
    }    

    
}
