using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveManager : Singleton<SaveManager>
{
    [ContextMenu("Module 10/ Lưu dữ liệu giả")]
    private void DebugSaveFake()
    {
        var d = new SaveData { version = SaveMigrator.CurrentVersion, savedAtUtc = System.DateTime.UtcNow.ToString("O") };
        d.player.coin = 123;
        d.player.sceneName = "FirstFloorScene";
        d.player.checkpointId = "F1_CP01";
        d.world.flags.Add("test_flag");
        d.inventory.slots.Add(new SlotSave { index = 0, itemId = "item_bread", quantity = 2 });
        Debug.Log(SaveSystem.Save(d) ? $"Đã lưu: {SaveSystem.FilePath}": "Lưu thất bại");
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

