using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// [Module 5] Một phòng: collider (dùng chung cho nhận diện phòng + Confiner2D) và vcam riêng.
/// Các collider con tên "Buffer..." là dải đệm lấn sang phòng kế bên: chỉ dùng để GIỮ phòng hiện tại lâu hơn.
[RequireComponent(typeof(Collider2D))]
public class Room : MonoBehaviour
{
    [SerializeField] private string roomId;                     // vd "F1_R03" — duy nhất toàn game
    [SerializeField] private CinemachineVirtualCamera vcam;

    private const int ActivePriority = 20;                      // cao hơn mọi vcam cũ (đang dùng 10)
    private const int InactivePriority = 0;

    public string RoomId => roomId;

    private readonly List<Collider2D> areas = new List<Collider2D>();

    /// Điểm p còn nằm trong phòng hoặc trong dải đệm của phòng không.
    public bool Contains(Vector2 p)
    {
        foreach (var a in areas) if (a != null && a.OverlapPoint(p)) return true;
        return false;
    }

    private void Awake()
    {
        areas.Add(GetComponent<Collider2D>());
        foreach (Transform child in transform)
            if (child.name.StartsWith("Buffer") && child.TryGetComponent(out Collider2D c)) areas.Add(c);
        if (vcam != null) vcam.Priority = InactivePriority;
        if (string.IsNullOrEmpty(roomId)) Debug.LogError($"{name}: chưa đặt Room Id", this);
    }

    public void Activate(Transform player)
    {
        if (vcam == null) { Debug.LogError($"{name}: chưa gán vcam", this); return; }
        vcam.Follow = player;          // Player ở scene khác → không kéo được trong Inspector, gán lúc runtime
        vcam.PreviousStateIsValid = false;   // camera đích nhảy ngay tới player, để Brain lo phần trượt
        vcam.Priority = ActivePriority;
    }

    public void Deactivate()
    {
        if (vcam != null) vcam.Priority = InactivePriority;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
        UnityEditor.Handles.Label(c.bounds.center, roomId);
    }
#endif
}
