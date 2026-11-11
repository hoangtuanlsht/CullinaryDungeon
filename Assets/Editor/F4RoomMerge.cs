using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Tầng 4 kiểu Hollow Knight (chép từ F2RoomMerge, chỉ đổi dữ liệu tầng):
///  1) Gộp phòng: các khung nối ngang cùng khu thành 1 Room (1 vcam). Khung gốc giữ thành con "RoomRect_<id>".
///  2) Tường dày: tầng 4 dựng hoàn toàn bằng FourthFloorBuilder nên ĐÁ HẦM ĐƯỢC ĐẮP TRONG BUILDER (đúng luật ghép tile của tầng).
///     Ở đây chỉ kiểm tra (phải ra "đắp 0") và đặt 1 vệt sáng giữa mỗi hầm.
///  3) Sửa đường tuần tra của quái (file F4Data/f4_enemy_fix.txt, tầng 4 chưa có quái → bỏ qua).
///  4) Gọi F4RoomFrames.Apply: khung camera, dải lấn, tường chặn quái ở chỗ chuyển phòng + vùng cấm quái quanh đó.
/// Chạy: Tools > Culinaria > F4 Merge Rooms + Tunnels (FourthFloorBuilder cũng tự gọi ở cuối). Chạy lại được.
public static class F4RoomMerge
{
    const string SceneName = "FourthFloorScene";
    const string BackupPath = "Assets/Scenes/Backup/FourthFloorScene_2026-11-11_beforeMerge.unity";
    const string GlowPath = "Assets/BackGround/ThirdFloor/F3_TunnelGlow.png";   // dùng chung texture với tầng 3
    const string GlowRoot = "______TunnelGlow_____";
    const string EnemyRoot = "______EnemyF4______";
    const string EnemyFixFile = "Assets/BackGround/FourthFloor/F4Data/f4_enemy_fix.txt";
    const int HalfLen = 4;
    const int DoorH = 3;                 // cửa ngang tầng 4 cao 3 ô
    const int MaxFill = 40;
    public static readonly Color GlowColor = new Color(0.55f, 1f, 0.95f, 0.35f);   // xanh ngọc của hồ phát sáng

    public static readonly (string leader, string[] members)[] Groups =
    {
        ("F4_R01", new[] { "F4_R02", "F4_R03" }),   // hang dẫn
        ("F4_R05", new[] { "F4_R06" }),             // bờ hồ
        ("F4_R07", new[] { "F4_R09" }),             // sảnh + khe Wall Jump 1
        ("F4_R10", new[] { "F4_R11" }),             // cầu gãy
        ("F4_R12", new[] { "F4_R13", "F4_R14" }),   // mặt hồ
        ("F4_R18", new[] { "F4_R19", "F4_R22" }),   // thành phố chìm, đường chính
        ("F4_R20", new[] { "F4_R21" }),             // quảng trường chìm + phòng rương 2
        ("F4_R24", new[] { "F4_R25" }),             // kênh đáy
        ("F4_R28", new[] { "F4_R29", "F4_R30" }),   // lòng cột trụ sau boss
    };

    /// (x vách chung, y sàn đứng của cửa) — phải khớp FourthFloorBuilder.KeepTunnels
    public static readonly (int bx, int yStand)[] Tunnels =
    {
        (108, -49),   // R07 | R08 (checkpoint 2)
        (90, -69),    // R11 | R12 (bờ hồ → mặt hồ)
        (126, -109),  // R16 | R17 (túi khí, checkpoint 3)
        (126, -129),  // R20 | R23 (túi khí, khoá Wall Jump 2)
        (144, -159),  // R25 | R26 (checkpoint 4)
        (162, -159),  // R26 | R27 (phòng boss)
    };

    [MenuItem("Tools/Culinaria/F4 Merge Rooms + Tunnels")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("F4 Merge", "Hãy mở FourthFloorScene trước.", "OK");
            return;
        }
        EditorSceneManager.SaveScene(scene);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Backup")) AssetDatabase.CreateFolder("Assets/Scenes", "Backup");
            AssetDatabase.CopyAsset(scene.path, BackupPath);
            Debug.Log("[F4Merge] Đã sao lưu scene: " + BackupPath);
        }
        ApplyAll(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ApplyAll(Scene scene)
    {
        MergeRooms(scene);
        BuildTunnels(scene);
        FixEnemies(scene);
        F4RoomFrames.Apply(scene);
    }

    // ===================== 1) GỘP PHÒNG =====================
    static Object Vcam(Room r) => new SerializedObject(r).FindProperty("vcam").objectReferenceValue;

    static void MergeRooms(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var rooms = new Dictionary<string, Room>();
        foreach (var rm in roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)))
            foreach (var (rid, _) in F4RoomFrames.RoomRects(rm)) rooms[rid] = rm;
        var behaviours = roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToList();
        int merged = 0, retargeted = 0;

        foreach (var (leader, members) in Groups)
        {
            if (!rooms.TryGetValue(leader, out var L)) { Debug.LogWarning($"[F4Merge] Không thấy phòng {leader}"); continue; }
            F4RoomFrames.EnsureRoomRect(L);
            var lv = Vcam(L) as Component;

            foreach (var id in members)
            {
                if (!rooms.TryGetValue(id, out var M) || M == null || M == L) continue;
                F4RoomFrames.EnsureRoomRect(M);
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
                        Debug.Log($"[F4Merge] {b.gameObject.name}.{b.GetType().Name}.{it.propertyPath}: {id} → {leader}");
                    }
                    if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(b); }
                }
                Object.DestroyImmediate(M.gameObject);
                merged++;
            }
        }
        int left = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true)).Count();
        Debug.Log($"[F4Merge] Gộp {merged} phòng, đổi {retargeted} tham chiếu. Còn {left} phòng.");
    }

    // ===================== 2) TƯỜNG DÀY + VỆT SÁNG =====================
    static void BuildTunnels(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var ground = roots.SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).FirstOrDefault(t => t.name == "F4_Ground");
        if (ground == null) { Debug.LogError("[F4Merge] Thiếu tilemap F4_Ground"); return; }
        // Đá hầm đã do FourthFloorBuilder đắp. Ở đây chỉ đếm ô còn trống phía trên cửa trong đoạn hầm → phải bằng 0.
        foreach (var (bx, ys) in Tunnels)
        {
            int open = 0;
            for (int x = bx - HalfLen; x < bx + HalfLen; x++)
                if (!ground.HasTile(new Vector3Int(x, ys + DoorH, 0))) open++;
            if (open > 0) Debug.LogWarning($"[F4Merge] Hầm x={bx} y={ys}: còn {open} cột chưa đắp → chạy lại Build Fourth Floor (F4)");
            else Debug.Log($"[F4Merge] Hầm x={bx} y={ys}: đắp 0 ô (builder đã đắp)");
        }

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
    }

    // ===================== 3) ĐƯỜNG TUẦN TRA CỦA QUÁI =====================
    /// Mỗi dòng: chỉ số nhóm, phòng, loại, x, y chân, tường trái, tường phải (đã cắt bỏ vùng chuyển phòng).
    static void FixEnemies(Scene scene)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EnemyRoot);
        if (root == null || !File.Exists(EnemyFixFile)) { Debug.LogWarning("[F4Merge] Không có nhóm quái hoặc file sửa tuần tra"); return; }
        int moved = 0, n = 0;
        foreach (var line in File.ReadAllLines(EnemyFixFile))
        {
            var p = line.Split(' ');
            if (p.Length < 7) continue;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string suffix = "_" + int.Parse(p[0]).ToString("00");
            float x = float.Parse(p[3], ic), feet = float.Parse(p[4], ic), wl = float.Parse(p[5], ic), wr = float.Parse(p[6], ic);
            var g = root.transform.Cast<Transform>().FirstOrDefault(t => t.name.EndsWith(suffix) && t.name.StartsWith(p[1]));
            if (g == null) continue;
            n++;
            foreach (Transform c in g)
            {
                Vector3 pos = c.position;
                if (c.name == "EnemyWall_L") pos.x = wl;
                else if (c.name == "EnemyWall_R") pos.x = wr;
                else pos.x = x;   // quái + thanh máu
                if (Mathf.Abs(pos.x - c.position.x) > 0.01f) { c.position = pos; moved++; EditorUtility.SetDirty(c); }
            }
        }
        Debug.Log($"[F4Merge] Tuần tra quái: kiểm {n} nhóm, dời {moved} vật");
    }

    public static Sprite GlowSprite()
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
        if (s != null) return s;
        return F3RoomMerge.GlowSprite();
    }
}
