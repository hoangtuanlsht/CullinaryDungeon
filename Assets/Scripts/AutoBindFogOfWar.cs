using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoBindFogOfWar : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if(player!=null)
        {
            transform.SetParent(player.transform);
        }
    }

    
}
