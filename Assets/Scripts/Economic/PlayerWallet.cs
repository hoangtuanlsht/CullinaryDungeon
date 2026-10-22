using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
public static class PlayerWallet
{
    private const string SaveKey = "coin";
    public static int Coin { get; private set; }
    public static event Action<int> OnCoinChanged;

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Coin += amount;
        OnCoinChanged?.Invoke(Coin);
    }
    public static bool TrySpend(int amount)
    {
        if (amount < 0 || Coin < amount) return false;
        Coin -= amount;
        OnCoinChanged?.Invoke(Coin);
        return true;
    }
    /// [Module 10] Nạp từ save, hoặc New Game.
    public static void Restore(int coin)
    {
        Coin = Mathf.Max(0, coin);
        OnCoinChanged?.Invoke(Coin);
    }
    // [Module 10] Migration: số xu cũ nằm trong PlayerPrefs (trước Module 10).
    private static string LegacyKey = "coin";
    /// Lấy số xu cũ (nếu có) rồi XOÁ khoá đó, để chỉ chuyển đúng một lần.
    public static int TakeLegacyCoin()
    {
        if(!PlayerPrefs.HasKey(LegacyKey)) return 0;
        int coin = PlayerPrefs.GetInt(LegacyKey,0);
        PlayerPrefs.DeleteKey(LegacyKey);
        PlayerPrefs.Save();
        Debug.Log($"Chuyển {coin} xu từ PlayerPrefs sang save mới");
        return coin;
    }


}
