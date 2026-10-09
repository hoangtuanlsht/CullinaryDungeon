using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 7] Gắn trên object có Animator (Player/Anim).
/// Animation Event chỉ thấy hàm trên cùng GameObject → chuyển tiếp sang Player.
public class PlayerAnimEventRelay : MonoBehaviour
{
    [SerializeField] private Player player;

    private void Reset() => player = GetComponentInParent<Player>();   // tự điền khi Add Component

    public void AnimEvent_HitboxOn() => player.AnimEvent_HitboxOn();
    public void AnimEvent_HitboxOff() => player.AnimEvent_HitboxOff();
}