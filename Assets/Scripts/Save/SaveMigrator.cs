using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SaveMigrator
{
    public const int CurrentVersion = 2;

    /// Nâng cấp tại chỗ lên CurrentVersion. false nếu không dùng được (hỏng, hoặc mới hơn bản game).
    public static bool TryUpgrade(SaveData d)
    {
        if (d == null || d.version <= 0) return false;
        if (d.version > CurrentVersion)
        {
            Debug.LogWarning($"Save phiên bản {d.version}, game chỉ hiểu tới {CurrentVersion}: không nạp.");
            return false;
        }
        while (d.version < CurrentVersion)
        {
            switch (d.version)
            {
                case 1:
                    Migrate_1_to_2(d); break;
                default:
                    Debug.LogError($"Thiếu bước nâng cấp từ phiên bản {d.version}");
                    return false;
            }
        }
        return true;
    }
    /// v1 chưa có thời gian chơi. JsonUtility đã để 0; ghi rõ ra đây để mọi bước nâng cấp đều có một hàm,
    /// kể cả bước mà giá trị mặc định tình cờ đã đúng.
    private static void Migrate_1_to_2(SaveData d)
    {
        d.playTimeSecond = 0f;
        d.version = 2;
    }
    //MainMenu dùng để bật/tắt nút Continue.
    public static bool CanContinue() => SaveSystem.TryLoad(out SaveData d) && TryUpgrade(d);
}
