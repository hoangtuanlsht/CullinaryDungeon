using UnityEngine;

// Cho hiệu ứng lặp (đom đóm, lá rơi, bong bóng...) bắt đầu ở một khung hình ngẫu nhiên,
// để nhiều bản đặt cạnh nhau không chạy giống hệt nhau.
[RequireComponent(typeof(Animator))]
public class AnimRandomStart : MonoBehaviour
{
    void Start()
    {
        var anim = GetComponent<Animator>();
        if (anim == null || anim.runtimeAnimatorController == null) return;
        var state = anim.GetCurrentAnimatorStateInfo(0);
        anim.Play(state.fullPathHash, 0, Random.value);
    }
}
