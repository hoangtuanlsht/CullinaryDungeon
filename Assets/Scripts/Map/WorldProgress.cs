using System.Collections;
using System.Collections.Generic;
using System;

using UnityEngine;
using UnityEditor.PackageManager;

/// [Module 5] Tiến trình thế giới — hiện chỉ giữ trong bộ nhớ. Module 10 sẽ lưu ra JSON.
public static class WorldProgress
{
    private static readonly HashSet<string> visitedRooms = new HashSet<string>();
    private static readonly HashSet<string> flags = new HashSet<string>();

    public static event Action<string> OnRoomDiscovered;
    public static event Action<string> OnFlagSet;// [Module 10] SaveManager nghe để tự lưu

    public static bool HasFlag(string id) => !string.IsNullOrEmpty(id) && flags.Contains(id);
    public static void SetFlag(string id)
    {
        if (!string.IsNullOrEmpty(id)) flags.Add(id);
        if (flags.Add(id)) OnFlagSet?.Invoke(id);
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
    //-----------Module 10----------
    public static void CaptureTo(WorldSave dst)
    {
        dst.visitedRooms = new List<string>(visitedRooms);
        dst.flags = new List<string>(flags);
        dst.visitedRooms.Sort(StringComparer.Ordinal);       // HashSet không có thứ tự; sắp xếp để file dễ đọc, dễ so
        dst.flags.Sort(StringComparer.Ordinal);
    }
    /// Không đi qua MarkVisited/SetFlag: nạp lại không phải "khám phá mới", không được bắn event.
    public static void RestoreFrom(WorldSave src)
    {
        ResetAll();
        if(src.visitedRooms != null) visitedRooms.UnionWith(src.visitedRooms);
        if(src.flags != null) flags.UnionWith(src.flags);
    }
    /// New Game. KHÔNG xoá người đăng ký event (minimap, SaveManager vẫn đang sống).
    public static void ResetAll()
    {
        visitedRooms.Clear();
        flags.Clear();
    }

    // Nếu bật Enter Play Mode Options (tắt Domain Reload), static KHÔNG tự reset giữa các lần Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        ResetAll();
        OnRoomDiscovered = null;
        OnFlagSet = null;
    }
}
