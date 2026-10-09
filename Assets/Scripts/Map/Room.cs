using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// [Module 5] Một phòng: vùng giới hạn (collider, dùng chung cho nhận diện phòng + Confiner2D) và vcam riêng.
[RequireComponent(typeof(Collider2D))]
public class Room : MonoBehaviour
{
    [SerializeField] private string roomId;                     // vd "F1_R03" — duy nhất toàn game
    [SerializeField] private CinemachineVirtualCamera vcam;

    private const int ActivePriority = 20;                      // cao hơn mọi vcam cũ (đang dùng 10)
    private const int InactivePriority = 0;

    public string RoomId => roomId;

    private void Awake()
    {
        if (vcam != null) vcam.Priority = InactivePriority;
        if (string.IsNullOrEmpty(roomId)) Debug.LogError($"{name}: chưa đặt Room Id", this);
    }

    public void Activate(Transform player)
    {
        if (vcam == null) { Debug.LogError($"{name}: chưa gán vcam", this); return; }
        vcam.Follow = player;          // Player ở scene khác → không kéo được trong Inspector, gán lúc runtime
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
