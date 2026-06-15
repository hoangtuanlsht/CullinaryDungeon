using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryItems
{
    public string name { get; set; }
    public string description { get; set; }

    public Sprite itemIcon;
    public InventoryItems() { }
    public InventoryItems(string name, string description,Sprite itemIcon)
    {
        this.name = name;
        this.description = description;
        this.itemIcon = itemIcon;
    }

    public override string ToString()
    {
        return "Name: "+ this.name + " Des: " + this.description;
    }

    
}
