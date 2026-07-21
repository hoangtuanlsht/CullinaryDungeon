using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FogOfWarCheck : MonoBehaviour
{
    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Player player = collision.GetComponent<Player>();
            TextManager.hasPassedFogOfWar = true;
            Debug.Log("Player has passed the Fog of War.");
        }
    }
}
