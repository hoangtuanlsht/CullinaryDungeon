using System.Collections;
using System.Collections.Generic;
using System;

using UnityEngine;

/// [Module 5] Tiến trình thế giới — hiện chỉ giữ trong bộ nhớ. Module 10 sẽ lưu ra JSON.
public static class WorldProgress
{
    private static readonly HashSet<string> visitedRooms = new HashSet<string>();
    public static event Action<string> OnRoomDiscovered;
    private static readonly HashSet<string> flags = new HashSet<string>();

    public static bool HasFlag(string id) => flags.Contains(id);
    public static void SetFlag(string id)
    {
        if (!string.IsNullOrEmpty(id)) flags.Add(id);
    }
    public static IReadOnlyCollection<string> VisitedRooms => visitedRooms;
    public static bool IsVisited(string roomId) => visitedRooms.Contains(roomId);

    public static void MarkVisited(string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return;
        if (visitedRooms.Add(roomId))                 // Add trả false nếu đã có → chỉ báo lần đầu
        {
            Debug.Log($"Khám phá phòng mới: {roomId} ({visitedRooms.Count})");
            OnRoomDiscovered?.Invoke(roomId);
        }
    }

    // Nếu bật Enter Play Mode Options (tắt Domain Reload), static KHÔNG tự reset giữa các lần Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        visitedRooms.Clear();
        OnRoomDiscovered = null;
        flags.Clear();
    }
}
