using UnityEngine;

/// Cho mỗi hiệu ứng lặp (cột lửa, lửa ma, đầu lâu bay...) bắt đầu ở một khung ngẫu nhiên và chạy nhanh/chậm khác nhau một chút,
/// để nhiều hiệu ứng giống nhau trong cùng phòng không chạy đồng loạt như máy.
[RequireComponent(typeof(Animator))]
public class AnimRandomStart : MonoBehaviour
{
    [SerializeField] private float minSpeed = 0.85f;
    [SerializeField] private float maxSpeed = 1.15f;

    private void OnEnable()
    {
        var anim = GetComponent<Animator>();
        anim.Play(0, -1, Random.value);
        anim.speed = Random.Range(minSpeed, maxSpeed);
    }
}
