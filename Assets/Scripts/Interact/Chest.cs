using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chest : MonoBehaviour,Interact
{
    [SerializeField] private int coinAmount = 500;
    public void InteractWithObject()
    {
        Debug.Log("Mở rương! Nhận "+coinAmount+" vàng!");
        GetComponent<Collider2D>().enabled = false;
        SoundManager.Play("ChestOpen");
        Player.coin += coinAmount; 
        UIManager.instance.SetCoin(Player.coin); 
        Invoke("OnDestroy", 1f);
    }
    public void OnDestroy()
    {
        Destroy(gameObject); 
    }
}
