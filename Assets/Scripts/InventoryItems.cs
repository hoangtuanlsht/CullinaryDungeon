using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryItems
{
    public string name { get; set; }
    public string description { get; set; }
    public InventoryItems() { }
    public InventoryItems(string name, string description)
    {
        this.name = name;
        this.description = description;
    }

    public override string ToString()
    {
        return base.ToString();
    }

    
}
