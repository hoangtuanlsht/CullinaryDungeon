using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

/// [Module 5] Mỗi physics frame hỏi: "tâm Player đang nằm trong Room nào?"
public class RoomTracker : MonoBehaviour
{
    [SerializeField] private LayerMask roomLayer;     // chỉ tick "Room"
    [SerializeField] private Collider2D body;         // CapsuleCollider2D của Player

    public Room Current { get; private set; }
    public event Action<Room> OnRoomChanged;          // để dành cho minimap / nhạc nền theo khu

    private void Reset() => body = GetComponent<Collider2D>();

    private void FixedUpdate()
    {
        Collider2D hit = Physics2D.OverlapPoint(body.bounds.center, roomLayer);
        if (hit == null) return;                      // ra ngoài mọi phòng (lỗi map) → giữ phòng cũ

        Room room = hit.GetComponent<Room>();
        if (room == null || room == Current) return;

        // Nếu scene tầng cũ đã unload, Current là "null giả" của Unity → điều kiện dưới tự bỏ qua.
        if (Current != null) Current.Deactivate();
        Current = room;
        Current.Activate(transform);

        WorldProgress.MarkVisited(room.RoomId);       // B5
        OnRoomChanged?.Invoke(room);
    }
}
