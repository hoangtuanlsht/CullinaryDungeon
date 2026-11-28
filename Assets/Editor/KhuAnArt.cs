using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Trang trí khu ẩn (chạy SAU KhuAnBuilder): tile thật của tầng, tường nền, nền phòng mẹ, đồ trang trí, PixelFX.
///  - Tile: lấy mẫu tile có sẵn quanh khu ẩn theo "kiểu viền" (trống phía trên/dưới/trái/phải), ruột đá lấy theo khối 4×4 có sẵn.
///    Ghi thẳng vào tilemap mặt đất chính của tầng (bỏ khối màu F6_Block của KhuAn_Ground).
///  - Chỉ đổi tile trong khung phòng ẩn (+3 ô) và các vùng hầm/lỗ sàn khai báo ở đây. Không đụng RoomGlow/TunnelGlow/RoomGate.
///  - Tầng 1, 2 chưa có Light2D nào → bỏ Light2D trong prefab đuốc (tránh làm tối cả tầng).
/// Chạy: Tools > Culinaria > Khu An > Trang tri khu an. Chạy lại được (xóa đồ trang trí cũ của khu ẩn rồi đặt lại).
public static class KhuAnArt
{
    const string RootName = "______KhuAn_____";
    static readonly List<string> log = new List<string>();
    static Dictionary<string, Sprite> sprites;

    class Floor
    {
        public string bgParent;                 // phòng mẹ để chép nhóm nền (null = không chép)
        public Dictionary<string, string> bgParentByRoom = new Dictionary<string, string>();
        public string backWall;                 // tên (chứa) tilemap tường nền để lát lòng phòng ẩn (null = không)
        public RectInt[] extra = new RectInt[0];// vùng hầm / lỗ sàn ngoài khung phòng ẩn
        public bool noLights;
        public string[] lavaTiles;              // tile đáy hố Dash khi tầng không có DeadZone riêng
        public System.Action<Ctx> decor;
    }

    class Ctx
    {
        public Scene scene; public string tag; public Floor f; public Transform root;
        public Dictionary<string, RectInt> rooms = new Dictionary<string, RectInt>();
        public Dictionary<string, Transform> roomT = new Dictionary<string, Transform>();
        public Tilemap ground, plat; public Material mat;
        public int fx, props;
    }

    // ===================== DỮ LIỆU TỪNG TẦNG =====================
    static readonly Dictionary<string, Floor> Floors = new Dictionary<string, Floor>
    {
        { "F1", new Floor {
            backWall = "BackGround", noLights = true,
            extra = new[] { new RectInt(-41, -4, 10, 3) },
            decor = a => {
                FX(a, "F1_KA01_HanhLangTro", "torch", -70, -1, 2.2f);
                FX(a, "F1_KA01_HanhLangTro", "torch", -46, -1, 2.2f);
                FX(a, "F1_KA01_HanhLangTro", "soul-rise", -58.5f, -4.4f, 4.38f);
                FX(a, "F1_KA01_HanhLangTro", "fog", -58, -3.5f, 3.86f, 2f);
                FX(a, "F1_KA02_HamMo", "soul-rise", -86, -13.4f, 4.38f);
                FX(a, "F1_KA02_HamMo", "fog", -86, -13, 3.86f, 2f);
                FX(a, "F1_KA02_HamMo", "torch", -80, -1, 2.2f);
                Prop(a, "F1_KA02_HamMo", "g_coffinA", -88, -14); Prop(a, "F1_KA02_HamMo", "g_coffinB", -84, -14, true);
                Prop(a, "F1_KA02_HamMo", "g_candelabra", -92, -7);
                Reward(a, "F1_KA02_HamMo", -90.5f, -7);
            } } },
        { "F2", new Floor {
            bgParent = "F2_R09", noLights = true,
            extra = new[] { new RectInt(70, -121, 3, 8) },
            decor = a => {
                FX(a, "F2_KA01_TrucRe", "god-rays", 72, -126, 8f, 4f);
                FX(a, "F2_KA01_TrucRe", "fireflies", 72, -138, 7f);
                FX(a, "F2_KA01_TrucRe", "fireflies", 68, -146, 6.5f);
                FX(a, "F2_KA01_TrucRe", "drip", 66.5f, -122, 6.17f);
                FX(a, "F2_KA01_TrucRe", "drip", 77.5f, -122, 6.17f);
                FX(a, "F2_KA02_GocRe", "fireflies", 38, -146, 7f);
                FX(a, "F2_KA02_GocRe", "fireflies", 56, -146, 6.5f);
                FX(a, "F2_KA02_GocRe", "butterfly", 44, -145, 3.13f);
                FX(a, "F2_KA02_GocRe", "drip", 33.5f, -142, 6.17f);
                Reward(a, "F2_KA02_GocRe", 48.5f, -149);
            } } },
        { "F3", new Floor {
            bgParent = "F3_R05",
            extra = new[] { new RectInt(258, 71, 3, 3) },
            decor = a => {
                FX(a, "F3_KA01_GiengCong", "drip", 254.5f, 70, 6.17f);
                FX(a, "F3_KA01_GiengCong", "drip", 265.5f, 70, 6.17f);
                FX(a, "F3_KA01_GiengCong", "bubbles", 259, 56, 4.5f);
                FX(a, "F3_KA02_KenhThoatNuoc", "bubbles", 284, 55, 4.5f);
                FX(a, "F3_KA02_KenhThoatNuoc", "bubbles", 308, 56, 4.5f);
                FX(a, "F3_KA02_KenhThoatNuoc", "wisp", 292, 57, 3.13f);
                FX(a, "F3_KA02_KenhThoatNuoc", "fog", 300, 54, 3.86f, 2f);
                Hang(a, "F3_KA02_KenhThoatNuoc", "g_chain", 278, 61); Hang(a, "F3_KA02_KenhThoatNuoc", "g_chain", 314, 61);
                Reward(a, "F3_KA02_KenhThoatNuoc", 298.5f, 53);
            } } },
        { "F4", new Floor {
            bgParent = "F4_R24", backWall = "City_BackWall",
            extra = new[] { new RectInt(100, -161, 3, 12) },
            decor = a => {
                FX(a, "F4_KA01_GiengLuuTru", "bubbles", 100, -175, 4.5f);
                FX(a, "F4_KA01_GiengLuuTru", "jellyfish", 103, -167, 3.13f);
                FX(a, "F4_KA01_GiengLuuTru", "wisp", 96, -170, 3.13f);
                FX(a, "F4_KA02_KhoLuuTruChim", "fish-school", 138, -175, 7.59f, 4f);
                FX(a, "F4_KA02_KhoLuuTruChim", "jellyfish", 124, -176, 3.13f);
                FX(a, "F4_KA02_KhoLuuTruChim", "jellyfish", 156, -174, 3.13f);
                FX(a, "F4_KA02_KhoLuuTruChim", "bubbles", 146, -178, 4.5f);
                FX(a, "F4_KA02_KhoLuuTruChim", "wisp", 131, -173, 3.13f);
                Prop(a, "F4_KA02_KhoLuuTruChim", "g_bookshelf|g_shelfBooks|*shelf|*book", 123.5f, -174);
                Prop(a, "F4_KA02_KhoLuuTruChim", "g_bookshelf|g_shelfBooks|*shelf|*book", 137.5f, -174);
                Reward(a, "F4_KA02_KhoLuuTruChim", 150.5f, -179);
            } } },
        { "F5", new Floor {
            bgParent = "F5_R25",
            extra = new[] { new RectInt(285, -89, 4, 3) },
            lavaTiles = new[] { "dungeontilesetfull 1_298", "dungeontilesetfull 1_299" },
            decor = a => {
                FX(a, "F5_KA01_ViaSup", "torch", 293, -86, 2.2f);
                FX(a, "F5_KA01_ViaSup", "torch", 319, -86, 2.2f);
                FX(a, "F5_KA01_ViaSup", "flame-pillar", 305.5f, -90.2f, 2.5f);
                FX(a, "F5_KA02_OngKhoiRong", "brazier", 328, -99, 2f);
                FX(a, "F5_KA02_OngKhoiRong", "fog", 333, -92, 3.86f, 2f);
                FX(a, "F5_KA02_OngKhoiRong", "flame-pillar", 333, -99.2f, 2.2f);
                Prop(a, "F5_KA01_ViaSup", "s_rubble2", 296, -89); Prop(a, "F5_KA01_ViaSup", "s_rubble2", 315, -89, true);
                Prop(a, "F5_KA02_OngKhoiRong", "g_barrelSide", 340, -99);
                Hang(a, "F5_KA02_OngKhoiRong", "g_chain", 336, -81);
                Reward(a, "F5_KA02_OngKhoiRong", 335.5f, -99);
            } } },
        { "F6", new Floor {
            bgParent = "F6_R22",                // không lát tường nền: tường gạch che mất ảnh nền hầm mộ
            extra = new[] { new RectInt(203, -71, 3, 2) },
            decor = a => {
                // X1 cầu thang ấn phong
                FX(a, "F6_X1_CauThangAnPhong", "shadow-flames", 200.5f, -86, 3.5f);
                FX(a, "F6_X1_CauThangAnPhong", "shadow-flames", 213.5f, -78, 3.5f);
                Hang(a, "F6_X1_CauThangAnPhong", "g_webL", 200, -71);
                // X2 xưởng giả kim
                Prop(a, "F6_X2_XuongGiaKim", "g_stoneTable", 160, -79); Prop(a, "F6_X2_XuongGiaKim", "g_stoneTable", 178, -79);
                Prop(a, "F6_X2_XuongGiaKim", "g_barrelSide", 168, -79); Prop(a, "F6_X2_XuongGiaKim", "g_barrelSide", 186, -79, true);
                Prop(a, "F6_X2_XuongGiaKim", "g_candelabra", 155, -79);
                Hang(a, "F6_X2_XuongGiaKim", "g_chain", 164, -71); Hang(a, "F6_X2_XuongGiaKim", "g_chain", 182, -71);
                FX(a, "F6_X2_XuongGiaKim", "spirit-flame", 160, -77.5f, 3f);
                FX(a, "F6_X2_XuongGiaKim", "spirit-flame", 178, -77.5f, 3f);
                FX(a, "F6_X2_XuongGiaKim", "shadow-flames", 192, -75, 3.5f);
                // X3 phòng khế ước
                FX(a, "F6_X3_PhongKheUoc", "rune-circle", 171, -89, 5f);
                Prop(a, "F6_X3_PhongKheUoc", "g_floorCandles", 163, -89); Prop(a, "F6_X3_PhongKheUoc", "g_floorCandles", 179, -89, true);
                Hang(a, "F6_X3_PhongKheUoc", "g_chain", 158, -81); Hang(a, "F6_X3_PhongKheUoc", "g_chain", 184, -81);
                FX(a, "F6_X3_PhongKheUoc", "spirit-flame", 152, -88, 3f);
                FX(a, "F6_X3_PhongKheUoc", "spirit-flame", 194, -88, 3f);
                // X4 điện quỷ
                Prop(a, "F6_X4_DienQuy", "g_statue2", 150, -109); Prop(a, "F6_X4_DienQuy", "g_statue2", 210, -109, true);
                Hang(a, "F6_X4_DienQuy", "g_curtainRed", 158, -91); Hang(a, "F6_X4_DienQuy", "g_curtainRed", 202, -91);
                FX(a, "F6_X4_DienQuy", "shadow-flames", 154, -105, 3.5f);
                FX(a, "F6_X4_DienQuy", "shadow-flames", 206, -105, 3.5f);
                FX(a, "F6_X4_DienQuy", "flame-pillar", 170, -109.2f, 2.5f);
                FX(a, "F6_X4_DienQuy", "flame-pillar", 190, -109.2f, 2.5f);
                FX(a, "F6_X4_DienQuy", "rune-circle", 180, -109, 6f);
                FX(a, "F6_X4_DienQuy", "flying-skull", 180, -96, 2.6f);
                // mảnh 6 nằm trong hầm mộ R22 (phòng có sẵn)
                FXWorld(a, "key-glint", 200.5f, -68.4f, 3.13f);
                FXWorld(a, "loot-beam", 200.5f, -69, 3.75f);
            } } },
    };

    // ===================== MENU =====================
    [MenuItem("Tools/Culinaria/Khu An/Trang tri khu an")]
    public static void DecorateAll()
    {
        log.Clear(); sprites = null;
        string[] tags = { "F1", "F2", "F3", "F4", "F5", "F6" };
        for (int i = 0; i < 6; i++)
        {
            string path = KhuAnProbe.Scenes[i], tag = tags[i];
            Scene s = SceneManager.GetSceneByPath(path); bool opened = false;
            Scene prev = SceneManager.GetActiveScene();
            try
            {
                if (!s.isLoaded) { s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened = true; }
                else if (s.isDirty) { log.Add($"[{tag}] Scene đang mở và có thay đổi chưa lưu → bỏ qua."); continue; }
                SceneManager.SetActiveScene(s);
                if (Decorate(s, tag)) EditorSceneManager.SaveScene(s);
            }
            catch (System.Exception e) { log.Add($"[{tag}] LỖI: {e.GetType().Name}: {e.Message}"); Debug.LogException(e); }
            finally
            {
                if (prev.IsValid() && prev.isLoaded) SceneManager.SetActiveScene(prev);
                if (opened && s.isLoaded) EditorSceneManager.CloseScene(s, true);
            }
        }
        System.IO.File.WriteAllText("Temp/KhuAnArt.txt", string.Join("\n", log));
        foreach (var l in log) Debug.Log("[KhuAnArt] " + l);
    }

    public static bool Decorate(Scene s, string tag)
    {
        var f = Floors[tag];
        var rootGo = s.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (rootGo == null) { log.Add($"[{tag}] Chưa có {RootName} → chạy Build tat ca khu an trước."); return false; }
        var c = new Ctx { scene = s, tag = tag, f = f, root = rootGo.transform };
        var roomsT = c.root.Find("Rooms");
        if (roomsT == null) { log.Add($"[{tag}] Không có Rooms"); return false; }
        foreach (Transform t in roomsT)
        {
            var b = t.GetComponent<Collider2D>().bounds;
            c.rooms[t.name] = new RectInt(Mathf.RoundToInt(b.min.x), Mathf.RoundToInt(b.min.y), Mathf.RoundToInt(b.size.x), Mathf.RoundToInt(b.size.y));
            c.roomT[t.name] = t;
        }

        // Tilemap chính của tầng
        var all = s.GetRootGameObjects().Where(g => g.name != RootName).SelectMany(g => g.GetComponentsInChildren<Tilemap>(false)).ToList();
        int groundLayer = LayerMask.NameToLayer("Ground"), platLayer = LayerMask.NameToLayer("PlatForm");
        var colMaps = all.Where(t => t.TryGetComponent<TilemapCollider2D>(out var tc) && !tc.isTrigger).ToList();
        c.ground = colMaps.Where(t => t.GetComponent<PlatformEffector2D>() == null && !t.name.Contains("Plat")).OrderByDescending(Count).FirstOrDefault();
        c.plat = colMaps.Where(t => t.GetComponent<PlatformEffector2D>() != null || t.name.Contains("Plat")).OrderByDescending(Count).FirstOrDefault();
        if (c.ground == null) { log.Add($"[{tag}] Không thấy tilemap mặt đất"); return false; }
        c.mat = c.ground.GetComponent<TilemapRenderer>().sharedMaterial;

        var region = Bbox(c.rooms.Values.Concat(f.extra), 3);
        Reskin(c, region, all);
        BackWall(c, all);
        Backgrounds(c);

        // Đồ trang trí: xóa cũ, đặt lại
        foreach (var t in c.roomT.Values)
        {
            var old = t.Find("MapFX"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var oldP = t.Find("Props"); if (oldP != null) Object.DestroyImmediate(oldP.gameObject);
        }
        var oldW = c.root.Find("MapFX_World"); if (oldW != null) Object.DestroyImmediate(oldW.gameObject);
        f.decor?.Invoke(c);

        EditorSceneManager.MarkSceneDirty(s);
        log.Add($"[{tag}] Xong: tilemap '{c.ground.name}', material '{(c.mat ? c.mat.name : "-")}', {c.fx} hiệu ứng, {c.props} đồ trang trí.");
        return true;
    }

    static int Count(Tilemap t) { t.CompressBounds(); return t.GetUsedTilesCount() == 0 ? 0 : t.cellBounds.size.x * t.cellBounds.size.y; }

    static RectInt Bbox(IEnumerable<RectInt> rs, int pad)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (var r in rs) { x0 = Mathf.Min(x0, r.xMin); y0 = Mathf.Min(y0, r.yMin); x1 = Mathf.Max(x1, r.xMax); y1 = Mathf.Max(y1, r.yMax); }
        return new RectInt(x0 - pad, y0 - pad, x1 - x0 + 2 * pad, y1 - y0 + 2 * pad);
    }

    // ===================== TILE =====================
    struct Cell { public TileBase t; public Matrix4x4 m; public Color col; }

    static void Reskin(Ctx c, RectInt region, List<Tilemap> all)
    {
        var ours = c.root.Find("KhuAn_Ground")?.GetComponent<Tilemap>();
        var oursPlat = c.root.Find("KhuAn_Platform_OneWay")?.GetComponent<Tilemap>();
        var oursDead = c.root.Find("KhuAn_DeadZone (hố Dash)")?.GetComponent<Tilemap>();
        var solidMaps = all.Where(t => t.TryGetComponent<TilemapCollider2D>(out var tc) && !tc.isTrigger && !t.TryGetComponent<PlatformEffector2D>(out _) && !t.name.Contains("Plat")).ToList();
        bool Solid(int x, int y)
        {
            var p = new Vector3Int(x, y, 0);
            return solidMaps.Any(t => t.HasTile(p)) || (ours != null && ours.HasTile(p));
        }
        // Ô trống chỉ tính là "không khí" khi nằm trong khung một phòng (có sẵn hoặc ẩn) hoặc hầm; khoảng trống ngoài map coi như đá đặc
        var roomBoxes = c.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<global::Room>(true)).Select(r => r.GetComponent<Collider2D>().bounds).ToList();
        bool Air(int x, int y)
        {
            if (Solid(x, y)) return false;
            float px = x + 0.5f, py = y + 0.5f;
            foreach (var b in roomBoxes) if (px > b.min.x && px < b.max.x && py > b.min.y && py < b.max.y) return true;
            foreach (var r in c.f.extra) if (r.Contains(new Vector2Int(x, y))) return true;
            return false;
        }
        int Sig(int x, int y) => (Air(x, y + 1) ? 1 : 0) | (Air(x, y - 1) ? 2 : 0) | (Air(x - 1, y) ? 4 : 0) | (Air(x + 1, y) ? 8 : 0);

        // Mẫu: tile có sẵn của tilemap chính trong vùng quanh khu ẩn, nhóm theo kiểu viền
        RestoreFromBackup(c, region);
        var sample = new RectInt(region.x - 40, region.y - 40, region.width + 80, region.height + 80);
        var pool = new Dictionary<int, List<Cell>>();
        var bounds = c.ground.cellBounds;
        for (int x = Mathf.Max(sample.xMin, bounds.xMin); x < Mathf.Min(sample.xMax, bounds.xMax); x++)
            for (int y = Mathf.Max(sample.yMin, bounds.yMin); y < Mathf.Min(sample.yMax, bounds.yMax); y++)
            {
                if (region.Contains(new Vector2Int(x, y))) continue;           // chỉ học từ phần có sẵn của tầng
                var p = new Vector3Int(x, y, 0);
                var t = c.ground.GetTile(p); if (t == null) continue;
                int sg = Sig(x, y);
                if (!pool.TryGetValue(sg, out var l)) pool[sg] = l = new List<Cell>();
                l.Add(new Cell { t = t, m = c.ground.GetTransformMatrix(p), col = c.ground.GetColor(p) });
            }
        if (pool.Count == 0) { log.Add($"[{c.tag}] Không có tile mẫu quanh khu ẩn, giữ khối màu."); return; }

        // Ruột đá: khối 4×4 kín (kiểu 0) có sẵn → lát theo chu kỳ, giữ hoa văn
        Cell[,] block = FindBlock(c.ground, sample, region, (x, y) => Solid(x, y) && Sig(x, y) == 0, 4);
        Cell Pick(int x, int y, int sg)
        {
            if (sg == 0 && block != null) return block[Mod(x, 4), Mod(y, 4)];
            if (!pool.TryGetValue(sg, out var l) || l.Count == 0)
            {
                int[] fall = { sg & 1, sg & 2, 0 };
                l = fall.Select(k => pool.TryGetValue(k, out var q) ? q : null).FirstOrDefault(q => q != null && q.Count > 0) ?? pool.Values.First(q => q.Count > 0);
            }
            // ưu tiên tile phổ biến nhất của kiểu viền đó, thỉnh thoảng đổi biến thể
            var groups = l.GroupBy(q => q.t).OrderByDescending(g => g.Count()).Take(4).ToList();
            int h = Mathf.Abs(x * 73856093 ^ y * 19349663) % 10;
            var g0 = groups[h < 6 || groups.Count == 1 ? 0 : 1 + h % (groups.Count - 1)];
            return g0.First();
        }

        // 1) Chuyển ô khối màu của khu ẩn sang tilemap chính, đổi sang tile thật
        int moved = 0, re = 0;
        var oursCells = new List<Vector3Int>();
        if (ours != null) foreach (var p in ours.cellBounds.allPositionsWithin) if (ours.HasTile(p)) oursCells.Add(p);
        var assign = new Dictionary<Vector3Int, Cell>();
        foreach (var p in oursCells) assign[p] = Pick(p.x, p.y, Sig(p.x, p.y));
        // 2) Ô có sẵn trong vùng khu ẩn giáp chỗ trống (mặt đá mới lộ ra khi đục)
        for (int x = region.xMin; x < region.xMax; x++)
            for (int y = region.yMin; y < region.yMax; y++)
            {
                var p = new Vector3Int(x, y, 0);
                if (!c.ground.HasTile(p) || assign.ContainsKey(p)) continue;
                int sg = Sig(x, y); if (sg == 0) continue;
                if (!InHidden(c, x, y, Solid)) continue;
                assign[p] = Pick(x, y, sg); re++;
            }
        foreach (var kv in assign)
        {
            c.ground.SetTile(kv.Key, kv.Value.t);
            c.ground.SetTileFlags(kv.Key, TileFlags.None);
            c.ground.SetTransformMatrix(kv.Key, kv.Value.m);
            c.ground.SetColor(kv.Key, kv.Value.col);
        }
        if (ours != null) { foreach (var p in oursCells) ours.SetTile(p, null); moved = oursCells.Count; }
        EditorUtility.SetDirty(c.ground);

        // 3) Bậc một chiều: tile phổ biến nhất của tilemap bậc của tầng
        if (oursPlat != null && c.plat != null)
        {
            var platPool = new Dictionary<int, List<TileBase>>();
            c.plat.CompressBounds();
            foreach (var p in c.plat.cellBounds.allPositionsWithin)
            {
                var t = c.plat.GetTile(p); if (t == null) continue;
                int k = (c.plat.HasTile(p + Vector3Int.left) ? 0 : 1) | (c.plat.HasTile(p + Vector3Int.right) ? 0 : 2);
                if (!platPool.TryGetValue(k, out var l)) platPool[k] = l = new List<TileBase>();
                l.Add(t);
            }
            TileBase Top(int k) => (platPool.TryGetValue(k, out var l) ? l : platPool.Values.SelectMany(v => v).ToList()).GroupBy(t => t).OrderByDescending(g => g.Count()).First().Key;
            foreach (var p in oursPlat.cellBounds.allPositionsWithin)
            {
                if (!oursPlat.HasTile(p)) continue;
                int k = (oursPlat.HasTile(p + Vector3Int.left) ? 0 : 1) | (oursPlat.HasTile(p + Vector3Int.right) ? 0 : 2);
                oursPlat.SetTile(p, Top(k)); oursPlat.SetColor(p, Color.white);
            }
            var pr = oursPlat.GetComponent<TilemapRenderer>(); var srcR = c.plat.GetComponent<TilemapRenderer>();
            pr.sharedMaterial = srcR.sharedMaterial; pr.sortingOrder = srcR.sortingOrder; pr.sortingLayerID = srcR.sortingLayerID;
            EditorUtility.SetDirty(oursPlat);
        }

        // 4) Đáy hố Dash
        if (oursDead != null)
        {
            var dz = all.FirstOrDefault(t => t.CompareTag("DeadZone") && t.GetComponent<TilemapCollider2D>() != null && t.transform.root.name != RootName && t.name.Contains("DeadZone"));
            List<TileBase> lava = null;
            if (c.f.lavaTiles != null) lava = c.f.lavaTiles.Select(FindTile).Where(t => t != null).ToList();
            else if (dz != null)
            {
                dz.CompressBounds();
                var dzCells = new List<Vector3Int>(); foreach (var q in dz.cellBounds.allPositionsWithin) dzCells.Add(q);
                lava = dzCells.Where(p => dz.HasTile(p) && !dz.HasTile(p + Vector3Int.up)).Select(p => dz.GetTile(p))
                         .GroupBy(t => t).OrderByDescending(g => g.Count()).Take(2).Select(g => g.Key).ToList();
            }
            if (lava != null && lava.Count > 0)
            {
                foreach (var p in oursDead.cellBounds.allPositionsWithin)
                    if (oursDead.HasTile(p)) { oursDead.SetTile(p, lava[Mod(p.x, lava.Count)]); oursDead.SetColor(p, Color.white); }
                var dr = oursDead.GetComponent<TilemapRenderer>();
                dr.sharedMaterial = dz != null ? dz.GetComponent<TilemapRenderer>().sharedMaterial : c.mat;
                EditorUtility.SetDirty(oursDead);
            }
        }
        log.Add($"[{c.tag}] Tile: {moved} ô khối màu → tile thật, đổi {re} ô đá lộ mặt; mẫu {pool.Sum(p => p.Value.Count)} ô, ruột đá {(block != null ? "theo khối 4×4" : "theo tile phổ biến")}.");
    }

    /// Ô đá thuộc khu ẩn: giáp (4 hướng) một ô trống nằm TRONG lòng phòng ẩn hoặc trong hầm/lỗ khai báo. Ô giáp phòng cũ không đổi.
    static bool InHidden(Ctx c, int x, int y, System.Func<int, int, bool> solid)
    {
        foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
        {
            var n = new Vector2Int(x + d.x, y + d.y);
            if (solid(n.x, n.y)) continue;
            foreach (var r in c.rooms.Values) if (n.x > r.xMin && n.x < r.xMax - 1 && n.y > r.yMin && n.y < r.yMax - 1) return true;
            foreach (var r in c.f.extra) if (r.Contains(n)) return true;
        }
        return false;
    }

    /// Trả lại tile gốc (từ bản sao lưu trước khu ẩn) cho mọi ô trong vùng mà bản gốc và bản hiện tại đều có tile.
    /// Sửa các ô của phòng cũ bị đổi nhầm ở lần chạy trước; chạy lại bao nhiêu lần cũng được.
    static void RestoreFromBackup(Ctx c, RectInt region)
    {
        string bk = $"Assets/Scenes/Backup/{c.scene.name}_truocKhuAn.unity";
        if (!System.IO.File.Exists(bk)) { log.Add($"[{c.tag}] Không có bản sao lưu {bk}, bỏ qua khôi phục"); return; }
        string path = PathOf(c.ground.transform);
        var active = SceneManager.GetActiveScene();
        var b = EditorSceneManager.OpenScene(bk, OpenSceneMode.Additive);
        int n = 0;
        try
        {
            var src = b.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).FirstOrDefault(t => PathOf(t.transform) == path);
            if (src == null) { log.Add($"[{c.tag}] Bản sao lưu không có tilemap {path}"); return; }
            for (int x = region.xMin; x < region.xMax; x++)
                for (int y = region.yMin; y < region.yMax; y++)
                {
                    var p = new Vector3Int(x, y, 0);
                    var t0 = src.GetTile(p);
                    if (t0 == null || !c.ground.HasTile(p)) continue;
                    var m0 = src.GetTransformMatrix(p); var c0 = src.GetColor(p);
                    if (c.ground.GetTile(p) == t0 && c.ground.GetTransformMatrix(p) == m0 && c.ground.GetColor(p) == c0) continue;
                    c.ground.SetTile(p, t0); c.ground.SetTileFlags(p, TileFlags.None); c.ground.SetTransformMatrix(p, m0); c.ground.SetColor(p, c0); n++;
                }
        }
        finally
        {
            EditorSceneManager.CloseScene(b, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
        log.Add($"[{c.tag}] Khôi phục {n} ô tile gốc từ bản sao lưu.");
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    /// Tầng dùng material Lit: hiệu ứng lửa không có đèn sẵn thì gắn Light2D Point (màu theo loại lửa).
    static void AddLightIfNeeded(Ctx c, GameObject go, string prefab)
    {
        if (c.f.noLights || c.mat == null || !c.mat.name.Contains("Lit") || c.mat.name.Contains("Unlit")) return;
        if (go.GetComponentsInChildren<Component>(true).Any(k => k != null && k.GetType().Name == "Light2D")) return;
        Color col; float radius = 6f, intensity = 1f;
        switch (prefab)
        {
            case "shadow-flames": case "rune-circle": col = new Color(0.62f, 0.38f, 1f); break;
            case "spirit-flame": col = new Color(0.45f, 0.9f, 1f); radius = 5f; break;
            case "flame-pillar": col = new Color(1f, 0.55f, 0.2f); radius = 7f; intensity = 1.2f; break;
            case "loot-beam": col = new Color(1f, 0.9f, 0.5f); radius = 4f; intensity = 0.8f; break;
            default: return;
        }
        var lt = System.Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
        if (lt == null) { log.Add($"[{c.tag}] Không có Light2D (URP)"); return; }
        var lgo = new GameObject("Light");
        lgo.transform.SetParent(go.transform, false);
        lgo.transform.localScale = new Vector3(1f / go.transform.lossyScale.x, 1f / go.transform.lossyScale.y, 1f);
        var l = lgo.AddComponent(lt);
        var so = new SerializedObject(l);
        so.FindProperty("m_LightType").intValue = 3;   // Point
        so.FindProperty("m_Color").colorValue = col;
        so.FindProperty("m_Intensity").floatValue = intensity;
        so.FindProperty("m_PointLightOuterRadius").floatValue = radius;
        so.FindProperty("m_PointLightInnerRadius").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }


    static Cell[,] FindBlock(Tilemap tm, RectInt sample, RectInt skip, System.Func<int, int, bool> ok, int n)
    {
        var b = tm.cellBounds;
        for (int x = Mathf.Max(sample.xMin, b.xMin); x < Mathf.Min(sample.xMax, b.xMax) - n; x++)
            for (int y = Mathf.Max(sample.yMin, b.yMin); y < Mathf.Min(sample.yMax, b.yMax) - n; y++)
            {
                if (Mod(x, n) != 0 || Mod(y, n) != 0) continue;
                bool good = true;
                for (int i = 0; i < n && good; i++) for (int j = 0; j < n && good; j++)
                    if (skip.Contains(new Vector2Int(x + i, y + j)) || !tm.HasTile(new Vector3Int(x + i, y + j, 0)) || !ok(x + i, y + j)) good = false;
                if (!good) continue;
                var res = new Cell[n, n];
                for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
                {
                    var p = new Vector3Int(x + i, y + j, 0);
                    res[i, j] = new Cell { t = tm.GetTile(p), m = tm.GetTransformMatrix(p), col = tm.GetColor(p) };
                }
                return res;
            }
        return null;
    }

    static int Mod(int a, int n) => ((a % n) + n) % n;

    static TileBase FindTile(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:TileBase"))
        {
            var t = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g));
            if (t != null && t.name == name) return t;
        }
        return null;
    }

    // ===================== TƯỜNG NỀN =====================
    static void BackWall(Ctx c, List<Tilemap> all)
    {
        var old = c.root.Find("KhuAn_BackWall"); if (old != null) Object.DestroyImmediate(old.gameObject);
        if (c.f.backWall == null) return;
        var src = all.Where(t => t.name.Contains(c.f.backWall) && t.GetComponent<TilemapCollider2D>() == null).OrderByDescending(Count).FirstOrDefault();
        if (src == null) { log.Add($"[{c.tag}] Không thấy tilemap tường nền '{c.f.backWall}'"); return; }
        src.CompressBounds();
        Cell[,] blk = null;
        foreach (int n in new[] { 8, 4, 2 })
        {
            blk = FindBlock(src, new RectInt(src.cellBounds.xMin, src.cellBounds.yMin, src.cellBounds.size.x, src.cellBounds.size.y), new RectInt(0, 0, 0, 0), (x, y) => true, n);
            if (blk != null) break;
        }
        if (blk == null) { log.Add($"[{c.tag}] Tường nền '{src.name}' không có khối kín"); return; }
        int n2 = blk.GetLength(0);
        var go = new GameObject("KhuAn_BackWall", typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(c.root, false);
        var tm = go.GetComponent<Tilemap>(); var tr = go.GetComponent<TilemapRenderer>(); var sr = src.GetComponent<TilemapRenderer>();
        tr.sharedMaterial = sr.sharedMaterial; tr.sortingOrder = sr.sortingOrder; tr.sortingLayerID = sr.sortingLayerID;
        int k = 0;
        foreach (var r in c.rooms.Values)
            for (int x = r.xMin + 1; x < r.xMax - 1; x++)
                for (int y = r.yMin + 1; y < r.yMax - 1; y++)
                {
                    var cell = blk[Mod(x, n2), Mod(y, n2)];
                    var p = new Vector3Int(x, y, 0);
                    tm.SetTile(p, cell.t); tm.SetTileFlags(p, TileFlags.None); tm.SetTransformMatrix(p, cell.m); tm.SetColor(p, cell.col); k++;
                }
        log.Add($"[{c.tag}] Tường nền: {k} ô từ '{src.name}' (khối {n2}×{n2}).");
    }

    // ===================== NỀN PHÒNG MẸ =====================
    static void Backgrounds(Ctx c)
    {
        if (c.f.bgParent == null) return;
        var parent = c.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true)).FirstOrDefault(r => r.name == c.f.bgParent);
        if (parent == null) { log.Add($"[{c.tag}] Không thấy phòng mẹ {c.f.bgParent}"); return; }
        var pbg = new SerializedObject(parent).FindProperty("background").objectReferenceValue as GameObject;
        if (pbg == null) { log.Add($"[{c.tag}] {c.f.bgParent} không có nền"); return; }
        Vector2 pc = parent.GetComponent<Collider2D>().bounds.center;
        int n = 0;
        foreach (var kv in c.roomT)
        {
            var so = new SerializedObject(kv.Value.GetComponent<Room>());
            var cont = kv.Value.Find("BG_" + kv.Key);
            if (cont == null) continue;
            var oldCopy = cont.Find("NenPhongMe"); if (oldCopy != null) Object.DestroyImmediate(oldCopy.gameObject);
            var copy = Object.Instantiate(pbg);
            copy.name = "NenPhongMe";
            copy.SetActive(true);
            copy.transform.SetParent(cont, true);
            Vector2 hc = c.rooms[kv.Key].center;
            copy.transform.position = pbg.transform.position + (Vector3)(hc - pc);
            // container BG_<id>: giữ lớp tối dự phòng ở chính nó, thêm bản sao nền
            if (so.FindProperty("background").objectReferenceValue != cont.gameObject) { so.FindProperty("background").objectReferenceValue = cont.gameObject; so.ApplyModifiedPropertiesWithoutUndo(); }
            n++;
        }
        log.Add($"[{c.tag}] Nền: chép nền của {c.f.bgParent} cho {n} phòng ẩn.");
    }

    // ===================== HIỆU ỨNG, ĐỒ TRANG TRÍ =====================
    static Transform Child(Transform t, string name) { var f = t.Find(name); if (f != null) return f; var g = new GameObject(name); g.transform.SetParent(t, false); return g.transform; }

    static GameObject FXAt(Ctx c, Transform parent, string prefab, float x, float y, float scale, float z)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefab/pixelFX/{prefab}.prefab");
        if (pf == null) { log.Add($"[{c.tag}] Thiếu prefab pixelFX/{prefab}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = new Vector3(x, y, z);
        go.transform.localScale = Vector3.one * scale;
        if (c.f.noLights)
        {
            var lights = go.GetComponentsInChildren<Component>(true).Where(k => k != null && k.GetType().Name == "Light2D").ToList();
            if (lights.Count > 0)
            {
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var l in go.GetComponentsInChildren<Component>(true).Where(k => k != null && k.GetType().Name == "LightFlicker2D").ToList()) Object.DestroyImmediate(l);
                foreach (var l in go.GetComponentsInChildren<Component>(true).Where(k => k != null && k.GetType().Name == "Light2D").ToList()) Object.DestroyImmediate(l);
            }
        }
        AddLightIfNeeded(c, go, prefab);
        c.fx++;
        return go;
    }

    static void FX(Ctx c, string room, string prefab, float x, float y, float scale, float z = 0f)
    {
        if (!c.roomT.TryGetValue(room, out var t)) { log.Add($"[{c.tag}] FX: không có phòng {room}"); return; }
        FXAt(c, Child(t, "MapFX"), prefab, x, y, scale, z);
    }
    static void FXWorld(Ctx c, string prefab, float x, float y, float scale) => FXAt(c, Child(c.root, "MapFX_World"), prefab, x, y, scale, 0f);

    /// Mảnh Sự Thật: tia sáng phần thưởng + lấp lánh, ngay chỗ khối vàng đánh dấu.
    static void Reward(Ctx c, string room, float x, float stand)
    {
        FX(c, room, "loot-beam", x, stand, 3.75f);
        FX(c, room, "key-glint", x, stand + 0.6f, 3.13f);
    }

    static Sprite FindSprite(string names)
    {
        if (sprites == null)
        {
            sprites = new Dictionary<string, Sprite>();
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g), fn = System.IO.Path.GetFileNameWithoutExtension(p).ToLower();
                if (!(fn.Contains("tileset") || fn.Contains("props"))) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (o is Sprite sp && (sp.name.StartsWith("g_") || sp.name.StartsWith("s_")) && !sprites.ContainsKey(sp.name)) sprites[sp.name] = sp;
            }
        }
        foreach (var n in names.Split('|'))
        {
            if (n.StartsWith("*")) { var k = sprites.Keys.Where(s2 => s2.ToLower().Contains(n.Substring(1))).OrderBy(s2 => s2.Length).FirstOrDefault(); if (k != null) return sprites[k]; }
            else if (sprites.TryGetValue(n, out var s)) return s;
        }
        return null;
    }

    static GameObject MakeProp(Ctx c, string room, string names, float x, bool flip)
    {
        if (!c.roomT.TryGetValue(room, out var t)) { log.Add($"[{c.tag}] Prop: không có phòng {room}"); return null; }
        var sp = FindSprite(names);
        if (sp == null) { log.Add($"[{c.tag}] Không có sprite {names}, bỏ qua"); return null; }
        var go = new GameObject(sp.name);
        go.transform.SetParent(Child(t, "Props"), false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.flipX = flip; sr.sortingOrder = -5; sr.sharedMaterial = c.mat;
        go.transform.position = new Vector3(x, 0, 0);
        c.props++;
        return go;
    }
    /// Đặt đáy sprite đúng mặt đứng `stand`.
    static void Prop(Ctx c, string room, string names, float x, float stand, bool flip = false)
    {
        var go = MakeProp(c, room, names, x, flip); if (go == null) return;
        var b = go.GetComponent<SpriteRenderer>().bounds;
        go.transform.position += new Vector3(0, stand - b.min.y, 0);
    }
    /// Treo sprite sát trần: đỉnh sprite ở `ceil` (mặt dưới của trần).
    static void Hang(Ctx c, string room, string names, float x, float ceil)
    {
        var go = MakeProp(c, room, names, x, false); if (go == null) return;
        var b = go.GetComponent<SpriteRenderer>().bounds;
        go.transform.position += new Vector3(0, ceil - b.max.y, 0);
    }
}
