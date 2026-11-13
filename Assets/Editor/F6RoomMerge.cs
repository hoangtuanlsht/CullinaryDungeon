using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Tầng 6 kiểu Hollow Knight (chép từ F5RoomMerge, đổi dữ liệu tầng):
///  1) Gộp phòng: các khung nối ngang cùng khu thành 1 Room (1 vcam). Khung gốc giữ thành con "RoomRect_<id>".
///  2) Tường dày: đá đường hầm do SixthFloorBuilder.CarveTunnels đắp (dùng chung bảng Tunnels bên dưới). Ở đây chỉ đặt vệt sáng.
///  3) Gọi F6RoomFrames.Apply: khung camera, dải lấn, tường chặn quái + vùng cấm quái ở chỗ chuyển phòng.
/// SixthFloorBuilder.Build gọi ApplyAll ở cuối. Menu bên dưới chỉ chạy lại phần gộp + khung trên scene đã dựng.
public static class F6RoomMerge
{
    const string SceneName = "SixthFloorScene";
    const string BackupPath = "Assets/Scenes/Backup/SixthFloorScene_2026-11-12_beforeMerge.unity";
    const string GlowRoot = "______TunnelGlow_____";
    public const int HalfLen = 4;
    public const int DoorH = 3;                     // cửa ngang tầng 6 cao 3 ô (tầng 2/3: 4 ô)
    public static readonly Color GlowColor = new Color(1f, 0.55f, 0.30f, 0.35f);   // cam than hồng

    public static readonly (string leader, string[] members)[] Groups =
    {
        ("F6_R01", new[] { "F6_R02", "F6_R03", "F6_R04" }),            // hang dung nham trên
        ("F6_R06", new[] { "F6_R07", "F6_R08", "F6_R09" }),            // trục lò + hang dưới
        ("F6_R10", new[] { "F6_R12" }),                                // bờ hồ dung nham + cầu đá
        ("F6_R13", new[] { "F6_R14" }),                                // tháp cổng + tường thành
        ("F6_R15", new[] { "F6_R16", "F6_R17" }),                      // đại sảnh + hành lang tranh + bếp
        ("F6_R18", new[] { "F6_R19" }),                                // thư viện + tháp pháp sư
        ("F6_R20", new[] { "F6_R21" }),                                // nhà nguyện đổ + tháp canh
        ("F6_R23", new[] { "F6_R24" }),                                // ngục tối + hành lang sụp
        ("F6_R26", new[] { "F6_R27" }),                                // giếng tro + tiền phòng
    };

    /// (x vách chung, y sàn đứng của cửa ngang)
    public static readonly (int bx, int yStand)[] Tunnels =
    {
        (162, -19),   // R04 | R05 (vào trại thám hiểm)
        (198, -19),   // R05 | R06 (trại → trục lò)
        (108, -39),   // R09 | R10 (hang → bờ hồ dung nham)
        (18, -39),    // R12 | R13 (cầu đá → cổng lâu đài)
        (108, -49),   // R16 | R18 (hành lang tranh → thư viện)
        (108, -59),   // R17 | R18 (bếp → thư viện)
        (162, -59),   // R19 | R20 (tháp pháp sư → nhà nguyện)
        (144, -69),   // R25 | R19 (song sắt L1: phòng ngai ↔ đáy tháp)
        (108, -69),   // R24 | R25 (hành lang sụp → phòng ngai)
        (72, -99),    // R27 | R28 (tiền phòng → hang rồng)
    };

    [MenuItem("Tools/Culinaria/F6 Merge Rooms + Tunnels")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("F6 Merge", "Hãy mở SixthFloorScene trước.", "OK");
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
        Debug.Log("[F6Merge] Đã sao lưu scene: " + BackupPath);
    }

    public static void ApplyAll(Scene scene)
    {
        MergeRooms(scene);
        BuildTunnelGlows(scene);
        F6RoomFrames.Apply(scene);
    }

    // ===================== 1) GỘP PHÒNG =====================
    static Object Vcam(Room r) => new SerializedObject(r).FindProperty("vcam").objectReferenceValue;

    static void MergeRooms(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var rooms = new Dictionary<string, Room>();
        foreach (var rm in roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)))
            foreach (var (rid, _) in F6RoomFrames.RoomRects(rm)) rooms[rid] = rm;
        var behaviours = roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToList();
        int merged = 0, retargeted = 0;

        foreach (var (leader, members) in Groups)
        {
            if (!rooms.TryGetValue(leader, out var L)) { Debug.LogWarning($"[F6Merge] Không thấy phòng {leader}"); continue; }
            F6RoomFrames.EnsureRoomRect(L);
            var lv = Vcam(L) as Component;

            foreach (var id in members)
            {
                if (!rooms.TryGetValue(id, out var M) || M == null || M == L) continue;
                F6RoomFrames.EnsureRoomRect(M);
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
                        Debug.Log($"[F6Merge] {b.gameObject.name}.{b.GetType().Name}.{it.propertyPath}: {id} → {leader}");
                    }
                    if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(b); }
                }
                Object.DestroyImmediate(M.gameObject);
                merged++;
            }
        }
        int left = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true)).Count();
        Debug.Log($"[F6Merge] Gộp {merged} phòng, đổi {retargeted} tham chiếu. Còn {left} phòng.");
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
        Debug.Log($"[F6Merge] {Tunnels.Length} vệt sáng giữa hầm.");
    }

    public static Sprite GlowSprite() => F3RoomMerge.GlowSprite();
}
