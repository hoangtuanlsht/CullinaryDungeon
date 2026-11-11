using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Tầng 5 kiểu Hollow Knight (chép từ F2RoomMerge, đổi dữ liệu tầng):
///  1) Gộp phòng: các khung nối ngang cùng khu thành 1 Room (1 vcam). Khung gốc giữ thành con "RoomRect_<id>".
///  2) Tường dày: KHÁC tầng 2/3 — địa hình tầng 5 do FifthFloorBuilder sinh ra, nên đá đường hầm được đắp ngay trong
///     builder (CarveTunnels, dùng chung bảng Tunnels bên dưới) để luật tile của builder tự chọn hình. Ở đây chỉ đặt vệt sáng.
///  3) Không sửa tuần tra quái: tầng 5 chưa có quái thật (chỉ có điểm đánh dấu TODO).
///  4) Gọi F5RoomFrames.Apply: khung camera, dải lấn, tường chặn quái + vùng cấm quái ở chỗ chuyển phòng.
/// Mê cung R22–R41 tạm GIỮ NGUYÊN khung (người dùng duyệt ngày 11/11/2026: làm khu A/B/C trước, mê cung để sau).
/// FifthFloorBuilder.Build gọi ApplyAll ở cuối. Menu bên dưới chỉ chạy lại phần gộp + khung trên scene đã dựng.
public static class F5RoomMerge
{
    const string SceneName = "FifthFloorScene";
    const string BackupPath = "Assets/Scenes/Backup/FifthFloorScene_2026-11-11_beforeMerge.unity";
    const string GlowRoot = "______TunnelGlow_____";
    public const int HalfLen = 4;
    public const int DoorH = 3;                     // cửa ngang tầng 5 cao 3 ô (tầng 2/3: 4 ô)
    public static readonly Color GlowColor = new Color(1f, 0.82f, 0.55f, 0.35f);   // vàng cam ánh đuốc cho phố đá

    public static readonly (string leader, string[] members)[] Groups =
    {
        ("F5_R01", new[] { "F5_R02", "F5_R03", "F5_R04", "F5_R05", "F5_R06" }),   // trại orc
        ("F5_R07", new[] { "F5_R08" }),                                           // hầm nhiệm vụ
        ("F5_R10", new[] { "F5_R11", "F5_R12", "F5_R13" }),                       // phố trên
        ("F5_R16", new[] { "F5_R17", "F5_R18", "F5_R19", "F5_R20", "F5_R21" }),   // phố dưới
    };

    /// (x vách chung, y sàn đứng của cửa ngang)
    public static readonly (int bx, int yStand)[] Tunnels =
    {
        (72, -59),    // R13 | R15 (chợ cũ, Minotaurus)
        (270, -89),   // R21 | R22 (miệng cống → mê cung)
        (36, -139),   // R42 | R43 (tiền phòng → phòng boss)
    };

    [MenuItem("Tools/Culinaria/F5 Merge Rooms + Tunnels")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("F5 Merge", "Hãy mở FifthFloorScene trước.", "OK");
            return;
        }
        EditorSceneManager.SaveScene(scene);
        Backup(scene);
        ApplyAll(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void Backup(Scene scene)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) != null) return;
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Backup")) AssetDatabase.CreateFolder("Assets/Scenes", "Backup");
        AssetDatabase.CopyAsset(scene.path, BackupPath);
        Debug.Log("[F5Merge] Đã sao lưu scene: " + BackupPath);
    }

    public static void ApplyAll(Scene scene)
    {
        MergeRooms(scene);
        BuildTunnelGlows(scene);
        F5RoomFrames.Apply(scene);
    }

    // ===================== 1) GỘP PHÒNG =====================
    static Object Vcam(Room r) => new SerializedObject(r).FindProperty("vcam").objectReferenceValue;

    static void MergeRooms(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var rooms = new Dictionary<string, Room>();
        foreach (var rm in roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)))
            foreach (var (rid, _) in F5RoomFrames.RoomRects(rm)) rooms[rid] = rm;
        var behaviours = roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToList();
        int merged = 0, retargeted = 0;

        foreach (var (leader, members) in Groups)
        {
            if (!rooms.TryGetValue(leader, out var L)) { Debug.LogWarning($"[F5Merge] Không thấy phòng {leader}"); continue; }
            F5RoomFrames.EnsureRoomRect(L);
            var lv = Vcam(L) as Component;

            foreach (var id in members)
            {
                if (!rooms.TryGetValue(id, out var M) || M == null || M == L) continue;
                F5RoomFrames.EnsureRoomRect(M);
                foreach (var t in M.transform.Cast<Transform>().Where(t => t.name.StartsWith("RoomRect")).ToList())
                {
                    if (t.name == "RoomRect") t.name = "RoomRect_" + id;
                    t.SetParent(L.transform, true);
                }

                var mv = Vcam(M) as Component;
                var map = new Dictionary<Object, Object> { [M] = L, [M.gameObject] = L.gameObject };
                if (mv != null && lv != null)
                {
                    map[mv] = lv; map[mv.gameObject] = lv.gameObject; map[mv.transform] = lv.transform;
                }
                foreach (var b in behaviours)
                {
                    if (b == null || b.transform.IsChildOf(M.transform)) continue;
                    var so = new SerializedObject(b);
                    var it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                        if (!map.TryGetValue(it.objectReferenceValue, out var to)) continue;
                        it.objectReferenceValue = to; changed = true; retargeted++;
                        Debug.Log($"[F5Merge] {b.gameObject.name}.{b.GetType().Name}.{it.propertyPath}: {id} → {leader}");
                    }
                    if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(b); }
                }
                Object.DestroyImmediate(M.gameObject);
                merged++;
            }
        }
        int left = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true)).Count();
        Debug.Log($"[F5Merge] Gộp {merged} phòng, đổi {retargeted} tham chiếu. Còn {left} phòng.");
    }

    // ===================== 2) VỆT SÁNG GIỮA HẦM (đá đã do builder đắp) =====================
    static void BuildTunnelGlows(Scene scene)
    {
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == GlowRoot);
        if (old != null) Object.DestroyImmediate(old);
        var glowRoot = new GameObject(GlowRoot);
        SceneManager.MoveGameObjectToScene(glowRoot, scene);
        var spr = GlowSprite();
        foreach (var (bx, ys) in Tunnels)
        {
            var go = new GameObject($"Glow_{bx}_{ys}");
            go.transform.SetParent(glowRoot.transform, false);
            go.transform.position = new Vector3(bx, ys + DoorH / 2f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr; sr.color = GlowColor; sr.sortingOrder = -4;
            if (spr != null)
            {
                var s = spr.bounds.size;
                go.transform.localScale = new Vector3(3f / s.x, (DoorH + 1f) / s.y, 1f);
            }
            go.AddComponent<GlowPulse>();
        }
        Debug.Log($"[F5Merge] {Tunnels.Length} vệt sáng giữa hầm.");
    }

    public static Sprite GlowSprite() => F3RoomMerge.GlowSprite();
}
