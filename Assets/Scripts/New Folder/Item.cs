using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : Harvest
{

    public override void HarverstItem()
    {
        Debug.Log("Harvest");
        Destroy(this.gameObject);
    }
}
