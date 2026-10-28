using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chest : MonoBehaviour,Interact
{
    [SerializeField] private int coinAmount = 500;
    [Tooltip("Duy nhất toàn game, vd F3_Chest02")]
    [SerializeField] private string chestId;

    private bool opened;
    private void Start()
    {
        if (string.IsNullOrEmpty(chestId)) Debug.LogError($"{name}: Chưa đặt chestId", this);
        else if(WorldProgress.HasFlag(chestId)) gameObject.SetActive(false); // Đã mở, scene vừa load lại

    }
    public void InteractWithObject()
    {
        if (opened) return;
        opened = true;
        GetComponent<Collider2D>().enabled = false;
        SoundManager.Play("ChestOpen");
        PlayerWallet.Add(coinAmount);
        WorldProgress.SetFlag(chestId);
        Invoke("Remove", 1f);
    }
    private void Remove()
    {
        Destroy(gameObject); 
    }
}
