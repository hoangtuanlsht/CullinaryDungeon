using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : Singleton<UIManager>
{
    [SerializeField] TextMeshProUGUI coinText;
    public void SetCoin(int coin)
    {
        coinText.text = coin.ToString();
    }
    private void OnEnable()
    {
        PlayerWallet.OnCoinChanged += SetCoin;
        SetCoin(PlayerWallet.Coin); // late subscriber
    }
    private void OnDisable() { PlayerWallet.OnCoinChanged -= SetCoin; }
}