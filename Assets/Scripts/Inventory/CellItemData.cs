using PolyAndCode.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CellItemData : MonoBehaviour, ICell, IPointerClickHandler
{
    //UI
    [SerializeField]private Image itemIcon;
    //public TMP_Text desLabel;
    [SerializeField]private TMP_Text quanityLabel;
    //Model
    private InventoryManager _contactInfo;
    private int _cellIndex; 

    //This is called from the SetCell method in DataSource
    public void ConfigureCell(InventoryManager manager,SlotClass slotData, int cellIndex)
    {
        _cellIndex = cellIndex;
        _contactInfo = manager;

        if (slotData != null && slotData.GetItem() != null)
        {
            itemIcon.sprite = slotData.GetItem().itemIcon;
            itemIcon.enabled = true;
            quanityLabel.text = slotData.GetQuantity() > 1 ? slotData.GetQuantity().ToString() : "";
        }
        else
        {
            itemIcon.sprite = null;
            itemIcon.enabled = false;
            quanityLabel.text = "";
        }

    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Debug.Log("dd");
            _contactInfo.OnLeftClickSlot(_cellIndex);
        }
        else if(eventData.button == PointerEventData.InputButton.Right)
        {
            _contactInfo.OnRightClickSlot(_cellIndex);  
        }
    }
}
