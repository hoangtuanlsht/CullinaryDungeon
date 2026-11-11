using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Tầng 3 kiểu Hollow Knight:
///  1) Gộp phòng: các RoomDef nối ngang thành 1 Room (1 vcam). Khung chữ nhật gốc của phòng bị gộp được giữ
///     thành con "RoomRect_<id>" của phòng đầu nhóm; mọi tham chiếu tới vcam/Room cũ (checkpoint...) trỏ sang phòng đầu nhóm.
///  2) Tường dày: ở 4 ranh giới còn lại, cửa 4 ô thành đường hầm dài 2*HalfLen ô (đắp đá phía trên cửa, sát vách chung),
///     tile chọn lại theo luật của ThirdFloorBuilder. Hai miệng hầm có vệt sáng mờ nhấp nháy (GlowPulse).
///  3) Gọi F3RoomFrames.Apply: khung camera, dải lấn, tường chặn quái ở mọi chỗ chuyển phòng.
/// Chạy: Tools > Culinaria > F3 Merge Rooms + Tunnels. Chạy lại được (lần 2 không đổi gì).
public static class F3RoomMerge
{
    const string SceneName = "ThirdFloorScene";
    const string BackupPath = "Assets/Scenes/Backup/ThirdFloorScene_2026-11-10_beforeMerge.unity";
    const string GlowPath = "Assets/BackGround/ThirdFloor/F3_TunnelGlow.png";
    const string GlowRoot = "______TunnelGlow_____";
    const int HalfLen = 4;          // hầm dài 8 ô (vách cũ 2 ô → lấn thêm 3 ô vào mỗi phòng)
    const int MaxFill = 40;         // cột cao hơn thế = không có trần → bỏ qua
    public static readonly Color GlowColor = new Color(0.55f, 0.82f, 1f, 0.35f);

    const string KD = "Assets/TileMap/KnightDungeon/";
    const string CaveTileFolder = "Assets/TileMap/F3Cave/";

    /// Phòng đầu nhóm (giữ id) ← các phòng gộp vào
    public static readonly (string leader, string[] members)[] Groups =
    {
        ("F3_R01", new[] { "F3_R02" }),
        ("F3_R03", new[] { "F3_R04", "F3_R05", "F3_R06" }),
        ("F3_R09", new[] { "F3_R10" }),
        ("F3_R11", new[] { "F3_R12" }),
        ("F3_R13", new[] { "F3_R14", "F3_R15" }),
        ("F3_R17", new[] { "F3_R21", "F3_R22", "F3_R23", "F3_R24", "F3_R25" }),
    };

    /// Cửa ngang giữ ranh giới (x vách chung, y sàn đứng)
    public static readonly (int bx, int yStand)[] Tunnels =
    {
        (86, 19),     // R06 | R09
        (4, 73),      // R19 | R13
        (144, 73),    // R15 | R17
        (368, 73),    // R25 | R18 (cổng boss)
    };

    [MenuItem("Tools/Culinaria/F3 Merge Rooms + Tunnels")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("F3 Merge", "Hãy mở ThirdFloorScene trước.", "OK");
            return;
        }
        EditorSceneManager.SaveScene(scene);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null)
        {
            AssetDatabase.CopyAsset(scene.path, BackupPath);
            Debug.Log("[F3Merge] Đã sao lưu scene: " + BackupPath);
        }
        ApplyAll(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ApplyAll(Scene scene)
    {
        MergeRooms(scene);
        BuildTunnels(scene);
        F3RoomFrames.Apply(scene);
    }

    // ===================== 1) GỘP PHÒNG =====================
    static Object Vcam(Room r) => new SerializedObject(r).FindProperty("vcam").objectReferenceValue;

    static void MergeRooms(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        // Tìm phòng theo id KHUNG GỐC (nhận theo hình học), không theo tên → đổi tên phòng trong scene không làm gộp nhầm
        var rooms = new Dictionary<string, Room>();
        foreach (var rm in roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)))
            foreach (var (rid, _) in F3RoomFrames.RoomRects(rm)) rooms[rid] = rm;
        var behaviours = roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToList();
        int merged = 0, retargeted = 0;

        foreach (var (leader, members) in Groups)
        {
            if (!rooms.TryGetValue(leader, out var L)) { Debug.LogWarning($"[F3Merge] Không thấy phòng {leader}"); continue; }
            F3RoomFrames.EnsureRoomRect(L);
            var lv = Vcam(L) as Component;

            foreach (var id in members)
            {
                if (!rooms.TryGetValue(id, out var M) || M == null || M == L) continue;   // đã gộp ở lần chạy trước
                F3RoomFrames.EnsureRoomRect(M);
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
                        Debug.Log($"[F3Merge] {b.gameObject.name}.{b.GetType().Name}.{it.propertyPath}: {id} → {leader}");
                    }
                    if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(b); }
                }
                Object.DestroyImmediate(M.gameObject);
                merged++;
            }
        }
        int left = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true)).Count();
        Debug.Log($"[F3Merge] Gộp {merged} phòng, đổi {retargeted} tham chiếu. Còn {left} phòng.");
    }

    // ===================== 2) TƯỜNG DÀY + VỆT SÁNG =====================
    static int Hash(int x, int y) => Mathf.Abs((x * 73856093) ^ (y * 19349663));
    static TileBase LT(string p) { var t = AssetDatabase.LoadAssetAtPath<TileBase>(p); if (t == null) Debug.LogWarning("[F3Merge] Thiếu tile " + p); return t; }
    static TileBase Cave(int c, int r) => LT($"{CaveTileFolder}caveA_{c}_{r}.asset");

    static void BuildTunnels(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var maps = roots.SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).ToList();
        Tilemap M(string n) => maps.FirstOrDefault(t => t.name == n);
        var ground = M("F3_Ground"); var kg = M("F3_Ground_KnightDungeon");
        var lava = M("F3_DeadZone_Lava"); var decor = M("F3_Decor"); var plat = M("F3_Platform_OneWay");
        if (ground == null || kg == null) { Debug.LogError("[F3Merge] Thiếu tilemap F3_Ground / F3_Ground_KnightDungeon"); return; }

        bool Solid(int x, int y) { var p = new Vector3Int(x, y, 0); return ground.HasTile(p) || kg.HasTile(p); }
        bool Lava(int x, int y) => lava != null && lava.HasTile(new Vector3Int(x, y, 0));

        // Ô cần đắp: từng cột trong hầm, từ ngay trên cửa lên tới trần phòng
        var added = new List<(Vector3Int p, Tilemap tm)>();
        foreach (var (bx, ys) in Tunnels)
        {
            int n0 = added.Count;
            for (int x = bx - HalfLen; x < bx + HalfLen; x++)
            {
                var refCell = new Vector3Int(x < bx ? bx - 1 : bx, ys + 4, 0);   // ô vách cũ ngay trên cửa, cùng phía
                var tm = kg.HasTile(refCell) ? kg : ground;
                var col = new List<Vector3Int>();
                int y = ys + 4;
                while (!Solid(x, y) && col.Count < MaxFill) { col.Add(new Vector3Int(x, y, 0)); y++; }
                if (col.Count >= MaxFill) { Debug.LogWarning($"[F3Merge] Hầm x={bx}: cột {x} không có trần, bỏ qua"); continue; }
                foreach (var p in col) added.Add((p, tm));
            }
            Debug.Log($"[F3Merge] Hầm x={bx} y={ys}: đắp {added.Count - n0} ô");
        }

        if (added.Count > 0)
        {
            TileBase[] kBricks = { LT(KD + "Bricks1.asset"), LT(KD + "Bricks2.asset"), LT(KD + "Bricks3.asset"), LT(KD + "Bricks4.asset") };
            TileBase[] kFloor = { LT(KD + "Floor1.asset"), LT(KD + "Floor2.asset"), LT(KD + "Floor3.asset"), LT(KD + "Floor4.asset") };
            TileBase[] kCeil = { LT(KD + "Sealing1.asset"), LT(KD + "Sealing2.asset"), LT(KD + "Sealing3.asset"), LT(KD + "Sealing4.asset") };
            TileBase[] cSurf = Enumerable.Range(59, 10).Select(c => Cave(c, 61)).ToArray();
            TileBase[] cUnder = Enumerable.Range(59, 10).Select(c => Cave(c, 62)).ToArray();
            TileBase[] cCeil = Enumerable.Range(59, 10).Select(c => Cave(c, 78)).ToArray();
            TileBase[] cAboveCeil = Enumerable.Range(59, 10).Select(c => Cave(c, 77)).ToArray();
            TileBase[] cDeep = { Cave(45, 63), Cave(46, 63), Cave(45, 64), Cave(46, 64) };
            var half = Matrix4x4.Scale(new Vector3(0.5f, 0.5f, 1f));

            var addedSet = new HashSet<Vector3Int>(added.Select(a => a.p));
            foreach (var (p, tm) in added)
            {
                tm.SetTile(p, tm == kg ? kBricks[0] : cDeep[0]);   // tạm, chọn lại bên dưới
                if (plat != null) plat.SetTile(p, null);
                if (decor != null) decor.SetTile(p, null);
            }

            // Chỉ chọn lại tile cho ô mà luật của builder có nhìn tới ô vừa đắp (y±1, y±2, x±1)
            var offs = new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0), new Vector3Int(0, 2, 0),
                               new Vector3Int(0, -2, 0), new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0) };
            var touched = new HashSet<Vector3Int>();
            foreach (var p in addedSet) foreach (var o in offs) touched.Add(p - o);
            int retiled = 0;
            foreach (var q in touched)
            {
                int x = q.x, y = q.y, h = Hash(x, y);
                bool openAbove = !Solid(x, y + 1) && !Lava(x, y + 1);
                bool openBelow = !Solid(x, y - 1) && !Lava(x, y - 1);
                if (kg.HasTile(q))
                {
                    kg.SetTile(q, openAbove ? kFloor[h % 4] : openBelow ? kCeil[h % 4] : kBricks[h % 4]);
                    kg.SetTileFlags(q, TileFlags.None);
                    kg.SetTransformMatrix(q, half);
                    retiled++;
                }
                else if (ground.HasTile(q))
                {
                    bool openSide = !Solid(x - 1, y) || !Solid(x + 1, y);
                    TileBase ct = openAbove ? cSurf[h % 10]
                        : openBelow ? cCeil[h % 10]
                        : (!Solid(x, y + 2) && !Lava(x, y + 2)) || openSide ? cUnder[h % 10]
                        : !Solid(x, y - 2) ? cAboveCeil[h % 10]
                        : cDeep[h % 4];
                    if (ct != null) { ground.SetTile(q, ct); retiled++; }
                }
            }
            foreach (var tm in new[] { ground, kg })
            {
                var cc = tm.GetComponent<CompositeCollider2D>();
                if (cc != null) cc.GenerateGeometry();
                EditorUtility.SetDirty(tm);
            }
            Debug.Log($"[F3Merge] Đắp {added.Count} ô, chọn lại {retiled} tile");

            // Đồ trang trí / vật thể nằm trong phần đá mới
            bool InNew(Vector3 w) => addedSet.Contains(new Vector3Int(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y), 0));
            var props = roots.FirstOrDefault(g => g.name == "______Props_____");
            if (props != null)
            {
                var kill = new HashSet<GameObject>();
                foreach (var sr in props.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!InNew(sr.bounds.center)) continue;
                    var t = sr.transform;
                    while (t.parent != null && t.parent.parent != props.transform && t.parent != props.transform) t = t.parent;
                    if (t.parent != props.transform) kill.Add(t.gameObject);
                }
                foreach (var g in kill) { Debug.Log($"[F3Merge] Xóa đồ trang trí bị đá lấp: {g.name}"); Object.DestroyImmediate(g); }
            }
            foreach (var root in roots.Where(g => g != null && (g.name == "______Enemy_____" || g.name == "______Interact_____" || g.name == "______NPC_____" || g.name == "______Stairs_____")))
                foreach (Transform t in root.transform)
                    if (InNew(t.position)) Debug.LogWarning($"[F3Merge] {t.name} nằm trong đá mới ở {t.position}, cần dời tay");
        }

        // 1 vệt sáng ở GIỮA hầm = đúng ranh giới 2 phòng: camera mỗi bên dừng ở đây, đi qua thì chuyển phòng (tạo lại mỗi lần chạy)
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == GlowRoot);
        if (old != null) Object.DestroyImmediate(old);
        var glowRoot = new GameObject(GlowRoot);
        SceneManager.MoveGameObjectToScene(glowRoot, scene);
        var spr = GlowSprite();
        foreach (var (bx, ys) in Tunnels)
            foreach (int side in new[] { 0 })
            {
                var go = new GameObject($"Glow_{bx}");
                go.transform.SetParent(glowRoot.transform, false);
                go.transform.position = new Vector3(bx + side * HalfLen, ys + 2f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spr; sr.color = GlowColor; sr.sortingOrder = -4;   // sau đất (0), trước đồ trang trí (-5) và ảnh nền
                if (spr != null)
                {
                    var s = spr.bounds.size;
                    go.transform.localScale = new Vector3(3f / s.x, 5f / s.y, 1f);   // rộng 3 ô, cao 5 ô (đủ phủ lòng hầm 4 ô)
                }
                go.AddComponent<GlowPulse>();
            }
    }

    /// Texture vệt sáng: elip trắng mờ dần ra ngoài, lưu thành PNG một lần.
    public static Sprite GlowSprite()
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
        if (s != null) return s;
        const int w = 32, h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);            // smoothstep → mép rất mềm
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(GlowPath));
        File.WriteAllBytes(GlowPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(GlowPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(GlowPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
    }
}
