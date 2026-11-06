using System.Collections.Generic;
using UnityEngine;

/// [Tầng 5] Vùng bật fog of war. Đặt một trigger cho mỗi phòng tối (R22–R28).
/// Player ở trong ít nhất một vùng → bật lớp phủ fog; ra khỏi mọi vùng → tắt.
/// Dùng tập hợp thay vì bool để đi từ phòng tối này sang phòng tối kế bên không bị tắt rồi bật lại.
[RequireComponent(typeof(Collider2D))]
public class FogOfWarZone : MonoBehaviour
{
    [Tooltip("Lớp phủ fog sẽ bật/tắt (FogOfWarCanvas trong ______FogOfWar_____).")]
    [SerializeField] private GameObject fogOverlay;

    private static readonly HashSet<FogOfWarZone> occupied = new HashSet<FogOfWarZone>();

    private void Awake() => GetComponent<Collider2D>().isTrigger = true;

    private void Start() => Refresh();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        occupied.Add(this);
        Refresh();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        occupied.Remove(this);
        Refresh();
    }

    // Exit không được bảo đảm khi unload scene hay chết rồi hồi sinh → dọn ở OnDisable (bài học Module 8).
    private void OnDisable()
    {
        occupied.Remove(this);
        Refresh();
    }

    private void Refresh()
    {
        if (fogOverlay != null) fogOverlay.SetActive(occupied.Count > 0);
    }
}
