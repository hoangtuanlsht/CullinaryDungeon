using PolyAndCode.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CellItemData : MonoBehaviour,ICell
{
    //UI
    public TMP_Text nameLabel;
    public TMP_Text desLabel;
    public TMP_Text quanityLabel;
    //Model
    private InventoryItems _contactInfo;
    private int _cellIndex; 

    //This is called from the SetCell method in DataSource
    public void ConfigureCell(InventoryItems inventoryItems, int cellIndex)
    {
        _cellIndex = cellIndex;
        _contactInfo = inventoryItems;

        nameLabel.text = inventoryItems.name;
        desLabel.text = inventoryItems.description;
        quanityLabel.text = inventoryItems.description;
    }

}
