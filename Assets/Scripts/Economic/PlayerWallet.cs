using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
public static class PlayerWallet
{
    private const string SaveKey = "coin";
    public static int Coin { get; private set; }
    public static event Action<int> OnCoinChanged;

    public static void Load()
    {
        Coin = PlayerPrefs.GetInt(SaveKey, 0);
        OnCoinChanged?.Invoke(Coin);
    }
    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Coin += amount;
        SaveAndNotify();
    }
    public static bool TrySpend(int amount)
    {
        if (amount < 0 || Coin < amount) return false;
        Coin -= amount;
        SaveAndNotify();
        return true;
    }
    private static void SaveAndNotify()
    {
        PlayerPrefs.SetInt(SaveKey, Coin);
        OnCoinChanged?.Invoke(Coin);
    }
}
