using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

/// [Module 7] Mỗi kỹ năng là 1 bit. Thêm kỹ năng mới = thêm 1 dòng.
[Flags]
public enum Ability
{
    None = 0,
    DoubleJump = 1 << 0,
    WallJump = 1 << 1,
    GroundPound = 1 << 2,
}

/// [Module 7] Dữ liệu "đã mở khóa gì". Không biết gì về vật lý hay input.
[Serializable]
public class PlayerAbilities
{
    [SerializeField] private Ability unlocked = Ability.None;

    public event Action<Ability> OnUnlocked;      // UI thông báo, âm thanh... đăng ký ở đây
    public Ability Unlocked => unlocked;

    public bool Has(Ability ability) => ability != Ability.None && (unlocked & ability) == ability;

    /// Trả về true nếu đây là lần đầu mở kỹ năng này.
    public bool Unlock(Ability ability)
    {
        if (ability == Ability.None || Has(ability)) return false;
        unlocked |= ability;
        OnUnlocked?.Invoke(ability);
        return true;
    }

    /// Để dành Module 10 (nạp từ file save).
    public void Restore(Ability saved) => unlocked = saved;
}
