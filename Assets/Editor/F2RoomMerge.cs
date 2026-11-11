using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Tầng 2 kiểu Hollow Knight (chép từ F3RoomMerge, chỉ đổi dữ liệu tầng + luật tile):
///  1) Gộp phòng: các khung nối ngang cùng khu thành 1 Room (1 vcam). Khung gốc giữ thành con "RoomRect_<id>".
///  2) Tường dày: ở các ranh giới ngang còn lại, cửa thành đường hầm dài 2*HalfLen ô, 1 vệt sáng giữa hầm.
///     Tile chọn lại theo luật của F2LevelBuilderV3 (đất cỏ main_lev_buildA cho rừng, gạch xám cho tháp/đá).
///  3) Sửa đường tuần tra của quái (dữ liệu F2Data/f2_enemy_fix.txt): không quái nào đứng hoặc đi vào vùng chuyển phòng.
///  4) Gọi F2RoomFrames.Apply: khung camera, dải lấn, tường chặn quái ở chỗ chuyển phòng + vùng cấm quái quanh đó.
/// Chạy: Tools > Culinaria > F2 Merge Rooms + Tunnels. Chạy lại được (lần 2 không đổi gì).
public static class F2RoomMerge
{
    const string SceneName = "SecondFloorScene_v2";
    const string BackupPath = "Assets/Scenes/Backup/SecondFloorScene_v2_2026-11-11_beforeMerge.unity";
    const string GlowPath = "Assets/BackGround/ThirdFloor/F3_TunnelGlow.png";   // dùng chung texture với tầng 3
    const string GlowRoot = "______TunnelGlow_____";
    const string EnemyRoot = "______EnemyF2______";
    const string EnemyFixFile = "Assets/BackGround/SecondFloor/F2Data/f2_enemy_fix.txt";
    const int HalfLen = 4;
    const int MaxFill = 40;
    public static readonly Color GlowColor = new Color(0.75f, 1f, 0.7f, 0.35f);   // xanh lá nhạt cho rừng

    const string DunTile = "Assets/TileMap/dungeontilesetfull 1_{0}.asset";
    const string ForestTileDir = "Assets/TileMap/F2_Forest/";
    const string ForestSheet = "Assets/Resouces/main_lev_buildA.png";

    public static readonly (string leader, string[] members)[] Groups =
    {
        ("F2_R01", new[] { "F2_R02", "F2_R03" }),
        ("F2_R13", new[] { "F2_R12" }),
    };

    public static readonly (int bx, int yStand)[] Tunnels =
    {
        (144, -96),   // R03 | R05 (vào tán rừng)
        (198, -96),   // R05 | R07
        (252, -96),   // R07 | R10 (tàn tích, checkpoint)
        (288, -96),   // R10 | R11 (cổng boss)
        (342, -96),   // R11 | R16 (lối ra sau boss)
        (144, -116),  // R09 | R13
        (108, -77),   // R14 | R04 (tháp A)
        (240, -77),   // R08 | R15 (tháp B)
    };

    [MenuItem("Tools/Culinaria/F2 Merge Rooms + Tunnels")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("F2 Merge", "Hãy mở SecondFloorScene_v2 trước.", "OK");
            return;
        }
        EditorSceneManager.SaveScene(scene);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Backup")) AssetDatabase.CreateFolder("Assets/Scenes", "Backup");
            AssetDatabase.CopyAsset(scene.path, BackupPath);
            Debug.Log("[F2Merge] Đã sao lưu scene: " + BackupPath);
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
        F2RoomFrames.Apply(scene);
    }

    // ===================== 1) GỘP PHÒNG =====================
    static Object Vcam(Room r) => new SerializedObject(r).FindProperty("vcam").objectReferenceValue;

    static void MergeRooms(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var rooms = new Dictionary<string, Room>();
        foreach (var rm in roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)))
            foreach (var (rid, _) in F2RoomFrames.RoomRects(rm)) rooms[rid] = rm;
        var behaviours = roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToList();
        int merged = 0, retargeted = 0;

        foreach (var (leader, members) in Groups)
        {
            if (!rooms.TryGetValue(leader, out var L)) { Debug.LogWarning($"[F2Merge] Không thấy phòng {leader}"); continue; }
            F2RoomFrames.EnsureRoomRect(L);
            var lv = Vcam(L) as Component;

            foreach (var id in members)
            {
                if (!rooms.TryGetValue(id, out var M) || M == null || M == L) continue;
                F2RoomFrames.EnsureRoomRect(M);
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
                        Debug.Log($"[F2Merge] {b.gameObject.name}.{b.GetType().Name}.{it.propertyPath}: {id} → {leader}");
                    }
                    if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(b); }
                }
                Object.DestroyImmediate(M.gameObject);
                merged++;
            }
        }
        int left = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true)).Count();
        Debug.Log($"[F2Merge] Gộp {merged} phòng, đổi {retargeted} tham chiếu. Còn {left} phòng.");
    }

    // ===================== 2) TƯỜNG DÀY + VỆT SÁNG =====================
    static TileBase LT(string p) { var t = AssetDatabase.LoadAssetAtPath<TileBase>(p); if (t == null) Debug.LogWarning("[F2Merge] Thiếu tile " + p); return t; }
    static TileBase Dun(int id) => LT(string.Format(DunTile, id));
    static Dictionary<string, Sprite> forestSprites;
    static TileBase Forest(string cell)
    {
        string name = "caveA_" + cell, path = ForestTileDir + name + ".asset";
        var t = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        if (t != null) return t;
        if (forestSprites == null)
            forestSprites = AssetDatabase.LoadAllAssetsAtPath(ForestSheet).OfType<Sprite>().GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());
        if (!forestSprites.TryGetValue(name, out var sp)) { Debug.LogWarning("[F2Merge] Thiếu sprite " + name); return null; }
        var nt = ScriptableObject.CreateInstance<Tile>();
        nt.sprite = sp; nt.colliderType = Tile.ColliderType.Grid;
        AssetDatabase.CreateAsset(nt, path);
        return nt;
    }
    static T Pick<T>(T[] a, int x, int y) => a[Mathf.Abs(x * 7 + y * 13) % a.Length];

    static readonly string[] A_T = { "15_17", "16_17", "17_17", "18_17", "19_17", "20_17", "25_17" };
    static readonly string[] A_T2 = { "15_18", "16_18", "17_18", "18_18", "19_18", "20_18", "14_18", "26_18" };
    static readonly string[] A_C = { "14_22", "15_22", "16_22", "17_22", "18_22", "19_22", "20_22", "25_22" };
    static readonly string[] A_B = { "14_23", "15_23", "16_23", "17_23", "18_23", "19_23", "20_23", "25_23" };

    static void BuildTunnels(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var maps = roots.SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).Where(t => t.gameObject.activeInHierarchy).ToList();
        Tilemap M(string n) => maps.FirstOrDefault(t => t.name == n);
        var ground = M("F2_Ground"); var swamp = M("F2_Swamp");
        var decorF = M("F2_DecorFront"); var decorB = M("F2_DecorBack"); var plat = M("F2_Platform");
        if (ground == null) { Debug.LogError("[F2Merge] Thiếu tilemap F2_Ground"); return; }

        bool Solid(int x, int y) { var p = new Vector3Int(x, y, 0); return ground.HasTile(p) || (swamp != null && swamp.HasTile(p)); }
        bool Stone(Vector3Int q) { var t = ground.GetTile(q); return t != null && t.name.StartsWith("dungeontilesetfull"); }

        var added = new List<(Vector3Int p, bool stone)>();
        foreach (var (bx, ys) in Tunnels)
        {
            int n0 = added.Count;
            for (int x = bx - HalfLen; x < bx + HalfLen; x++)
            {
                var refCell = new Vector3Int(x < bx ? bx - 1 : bx, ys + 4, 0);
                // ô vách cũ trên cửa có thể đã bị khoét (cửa cao 6 hoặc 12 ô) → tìm ô đất gần nhất phía trên
                int ry = ys + 4; while (!ground.HasTile(new Vector3Int(refCell.x, ry, 0)) && ry < ys + MaxFill) ry++;
                bool stone = Stone(new Vector3Int(refCell.x, ry, 0));
                var col = new List<Vector3Int>();
                int y = ys + 4;
                while (!Solid(x, y) && col.Count < MaxFill) { col.Add(new Vector3Int(x, y, 0)); y++; }
                if (col.Count >= MaxFill) { Debug.LogWarning($"[F2Merge] Hầm x={bx}: cột {x} không có trần, bỏ qua"); continue; }
                foreach (var p in col) added.Add((p, stone));
            }
            Debug.Log($"[F2Merge] Hầm x={bx} y={ys}: đắp {added.Count - n0} ô");
        }

        var addedSet = new HashSet<Vector3Int>(added.Select(a => a.p));
        if (added.Count > 0)
        {
            foreach (var (p, stone) in added)
            {
                ground.SetTile(p, stone ? Dun(172) : Forest("14_22"));   // tạm, chọn lại bên dưới
                if (plat != null) plat.SetTile(p, null);
                if (decorF != null) decorF.SetTile(p, null);
                if (decorB != null) decorB.SetTile(p, null);
            }
            // cỏ/hoa đứng trên ô vừa thành đất cũng bị xoá
            if (decorF != null) foreach (var p in addedSet) { var a = p + Vector3Int.up; if (addedSet.Contains(a) || ground.HasTile(a)) decorF.SetTile(a, null); }

            var touched = new HashSet<Vector3Int>();
            for (int dx = -1; dx <= 1; dx++) for (int dy = -2; dy <= 2; dy++)
                foreach (var p in addedSet) touched.Add(p + new Vector3Int(dx, dy, 0));
            int retiled = 0;
            foreach (var q in touched)
            {
                if (!ground.HasTile(q)) continue;
                int x = q.x, y = q.y;
                bool u = Solid(x, y + 1), d = Solid(x, y - 1), l = Solid(x - 1, y), r = Solid(x + 1, y);
                TileBase t;
                if (Stone(q))
                {
                    int k;
                    if (!u && !d) k = (l && r) ? 235 : r ? 234 : l ? 236 : 237;
                    else if (!l && !r) k = !u ? 143 : !d ? 206 : 174;
                    else
                    {
                        int row = !u ? 0 : !d ? 2 : 1, colI = !l ? 0 : !r ? 2 : 1;
                        k = new[,] { { 140, 141, 142 }, { 171, 172, 173 }, { 203, 204, 205 } }[row, colI];
                    }
                    t = Dun(k);
                }
                else
                {
                    bool belowTop = !Solid(x, y + 2) && u;
                    string key = !u ? (!l ? "12_17" : !r ? "28_17" : Pick(A_T, x, y))
                               : !d ? (!l ? "12_23" : !r ? "27_23" : Pick(A_B, x, y))
                               : !l ? (belowTop ? "12_18" : "12_22")
                               : !r ? (belowTop ? "28_18" : "27_22")
                               : belowTop ? Pick(A_T2, x, y) : Pick(A_C, x, y);
                    t = Forest(key);
                }
                if (t != null && ground.GetTile(q) != t) { ground.SetTile(q, t); retiled++; }
            }
            var cc = ground.GetComponent<CompositeCollider2D>();
            if (cc != null) cc.GenerateGeometry();
            EditorUtility.SetDirty(ground);
            AssetDatabase.SaveAssets();
            Debug.Log($"[F2Merge] Đắp {added.Count} ô, chọn lại {retiled} tile");

            bool InNew(Vector3 w) => addedSet.Contains(new Vector3Int(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y), 0));
            var props = roots.FirstOrDefault(g => g.name == "F2_Props");
            if (props != null)
                foreach (Transform t in props.transform.Cast<Transform>().ToList())
                {
                    var sr = t.GetComponent<SpriteRenderer>();
                    if (sr != null && InNew(sr.bounds.center)) { Debug.Log($"[F2Merge] Xóa đồ trang trí bị đá lấp: {t.name}"); Object.DestroyImmediate(t.gameObject); }
                }
            foreach (var root in roots.Where(g => g != null && (g.name == EnemyRoot || g.name == "F2_Markers")))
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != root.transform && InNew(t.position)) Debug.LogWarning($"[F2Merge] {t.name} nằm trong đá mới ở {t.position}, cần dời tay");
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
            go.transform.position = new Vector3(bx, ys + 2f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr; sr.color = GlowColor; sr.sortingOrder = -4;
            if (spr != null)
            {
                var s = spr.bounds.size;
                go.transform.localScale = new Vector3(3f / s.x, 5f / s.y, 1f);
            }
            go.AddComponent<GlowPulse>();
        }
    }

    // ===================== 3) ĐƯỜNG TUẦN TRA CỦA QUÁI =====================
    /// Mỗi dòng: chỉ số nhóm, phòng, loại, x, y chân, tường trái, tường phải (đã cắt bỏ vùng chuyển phòng).
    static void FixEnemies(Scene scene)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EnemyRoot);
        if (root == null || !File.Exists(EnemyFixFile)) { Debug.LogWarning("[F2Merge] Không có nhóm quái hoặc file sửa tuần tra"); return; }
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
        Debug.Log($"[F2Merge] Tuần tra quái: kiểm {n} nhóm, dời {moved} vật");
    }

    public static Sprite GlowSprite()
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
        if (s != null) return s;
        return F3RoomMerge.GlowSprite();
    }
}
