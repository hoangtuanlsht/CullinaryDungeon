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
        Vector2 p = body.bounds.center;

        // Vùng đệm (hysteresis): các phòng cạnh nhau giao nhau một dải.
        // Còn đứng trong phòng hiện tại thì giữ nguyên, chỉ đổi khi đã ra hẳn khỏi nó
        // → đứng lắc qua lại ở ranh giới không làm camera nhảy liên tục.
        if (Current != null && Current.Contains(p)) return;

        int n = Physics2D.OverlapPoint(p, filter, hits);
        for (int i = 0; i < n; i++)
        {
            Room room = hits[i].GetComponent<Room>();   // bỏ qua collider con CamBounds (không có Room)
            if (room == null || room == Current) continue;

            // Nếu scene tầng cũ đã unload, Current là "null giả" của Unity → điều kiện dưới tự bỏ qua.
            if (Current != null) Current.Deactivate();
            Current = room;
            Current.Activate(transform);

            WorldProgress.MarkVisited(room.RoomId);   // B5
            OnRoomChanged?.Invoke(room);
            return;
        }
        // Không trúng phòng nào (lỗi map) → giữ phòng cũ.
    }


    private readonly Collider2D[] hits = new Collider2D[8];
    private ContactFilter2D filter;

    private void Awake()
    {
        filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(roomLayer);
    }
}
