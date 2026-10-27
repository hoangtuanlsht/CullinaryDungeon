using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 10] Chỗ DUY NHẤT biết hệ thống nào ứng với phần nào của SaveData.
/// Đặt trong PersistentGameplay.
public class SaveManager : Singleton<SaveManager>
{
    [SerializeField] private Player player;
    [SerializeField] private InventoryManager inventory;
    [SerializeField] private ItemDatabase itemDatabase;

    public event Action OnSaved;

    public bool IsLoading { get; set; }
    private bool dirty;
    // ---------- Tự lưu ----------

    private void OnEnable()
    {
        WorldProgress.OnFlagSet += HandleFlagSet;
        player.OnSavePointChanged += RequestSave;
        player.Abilities.OnUnlocked += HandleUnlock;
    }

    private void OnDisable()
    {
        WorldProgress.OnFlagSet -= HandleFlagSet; // event static: QUÊN dòng này là lỗi khi về menu rồi chơi lại
        player.OnSavePointChanged -= RequestSave;
        player.Abilities.OnUnlocked -= HandleUnlock;
    }

    private void HandleFlagSet(string _) => RequestSave();
    private void HandleUnlock(Ability _) => RequestSave();

    public void RequestSave()
    {
        if(!IsLoading) dirty = true;
    }
    private void LateUpdate()
    {
        if (!dirty) return;
        dirty = false;
        SaveNow();
    }
    public bool SaveNow()
    {
        if (string.IsNullOrEmpty(player.RespawnCheckPointId)){
            Debug.Log("Chưa chạm checkPoint nào: chưa lưu");
            return false;
        }
        bool ok = SaveSystem.Save(Capture());
        if (ok) OnSaved?.Invoke();
        return ok;
    }
    // ---------- Capture / Restore ----------
    public SaveData Capture()
    {
        var d = new SaveData()
        {
            version = SaveMigrator.CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("o")
        };
        d.player.coin = PlayerWallet.Coin;
        d.player.abilities = (int)player.Abilities.Unlocked;
        d.player.hunger = player.PlayerHunger.HungerLevel;
        d.player.damage = player.damage;
        d.player.sceneName = player.RespawnSceneName;
        d.player.checkpointId = player.RespawnCheckPointId;

        WorldProgress.CaptureTo(d.world);
        inventory.CaptureTo(d.inventory);
        return d;
    }
    /// Không đặt vị trí Player, không load scene: việc đó của GameBootstrap.
    public void Restore(SaveData d)
    {
        PlayerWallet.Restore(d.player.coin);
        player.Abilities.Restore((Ability)d.player.abilities);
        player.PlayerHunger.Restore(d.player.hunger);
        if(d.player.damage>0) player.damage = d.player.damage;
        
        WorldProgress.RestoreFrom(d.world);
        inventory.RestoreFrom(d.inventory, itemDatabase);

    }
    public void ResetForNewGame()
    {
        int legacy = SaveSystem.HasSave() ? 0 : PlayerWallet.TakeLegacyCoin();
        PlayerWallet.Restore(legacy);
        WorldProgress.ResetAll();
        // Kỹ năng, đói, túi đồ, sát thương: PersistentGameplay vừa load mới nên đã ở giá trị Inspector.
    }
    // ---------- Nút test ----------
    [ContextMenu("Module 10/Lưu ngay")]
    private void DebugSave() => Debug.Log(SaveNow() ? $"Đã lưu {SaveSystem.FilePath}" : "Không lưu");
    [ContextMenu("Module 10/Nạp vào game đang chạy")]
    private void DebugLoad()
    {
        if (SaveSystem.TryLoad(out SaveData d) && SaveMigrator.TryUpgrade(d))
        {
            Restore(d);
            Debug.Log("Đã nạp");
        }
        else Debug.Log("Không có save dùng được ");
    }
    [ContextMenu("Module 10/Đọc và in ra")]
    private void DebugPrint()
    {
        if (SaveSystem.TryLoad(out SaveData d)) Debug.Log(JsonUtility.ToJson(d, true));
        else Debug.Log("Không có save");
    }

    [ContextMenu("Module 10/Mở thư mục save")]
    private void DebugOpenFolder() => Application.OpenURL("file://" + Application.persistentDataPath);

    [ContextMenu("Module 10/Xóa save")]
    private void DebugDelete() { SaveSystem.DeleteSave(); Debug.Log("Đã xóa save"); }
}

