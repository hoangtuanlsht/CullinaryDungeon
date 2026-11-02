using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Dựng lại ThirdFloorScene (tầng 3):
/// Trạm tiếp tế → Mê cung nhũ đá → Hành lang kỵ sĩ → Hành lang đá → Boss Demon Slime.
/// - Mê cung + trạm tiếp tế: nền ảnh hang F3_CaveBG.png
/// - Hành lang kỵ sĩ + hành lang đá: tile KnightDungeon (tường/sàn/trần/nền), R21 giữ nền hall.png làm cổng
/// - Platform gỗ + xương: tile Dungeon2
/// Chạy: Tools > Culinaria > Build Third Floor (F3). Chạy lại bao nhiêu lần cũng được (xóa sạch rồi dựng lại).
public static class ThirdFloorBuilder
{
    // ===================== THAM SỐ CHỈNH NHANH =====================
    const string SceneNameExpected = "ThirdFloorScene";
    const int ClimbColumnWidth = 11;
    const int PlatformWidth = 3;
    const bool UseTopTile = true;
    const bool FlipPlatformTile = true;  // tile bậc đá 1_65 bị ngược → lật dọc
    const int CorridorRooms = 5;
    const int CorridorRoomW = 40;
    const int BO = CorridorRooms * CorridorRoomW;   // phòng boss + gác thượng dịch phải
    const float KnightBright = 1.0f;     // tường/sàn/cầu thang KnightDungeon: 1 = độ sáng gốc (giảm < 1 để tối hơn)
    const float KnightBgBright = 0.8f;   // nền gạch hơi tối hơn tường để mặt sàn nổi rõ
    const float StairW = 16f / 3f;       // 1 mảnh cầu thang KnightDungeon rộng 5.33 ô, lên 3 ô
    const int StairRise = 3;
    const string HallGateRoom = "F3_R21"; // phòng duy nhất giữ ảnh hall.png (cổng vào hành lang đá)

    static readonly Color GroundTint = new Color(0.62f, 0.70f, 0.86f);
    static readonly Color DecorTint = Color.white;

    const string CaveBgPath = "Assets/BackGround/ThirdFloor/F3_CaveBG.png";
    const string HallBgPath = "Assets/BackGround/ThirdFloor/hall.png";
    const string BossBgPath = "Assets/BackGround/ThirdFloor/boss.png";
    const string BrightMatPath = "Assets/BackGround/ThirdFloor/F3_KnightBright.mat";
    const string BgMatPath = "Assets/BackGround/ThirdFloor/F3_KnightBg.mat";
    const string TM = "Assets/TileMap/";
    const string KD = "Assets/TileMap/KnightDungeon/";
    const string D2 = "Assets/TileMap/Dungeon2/Dungeon Tile Set_";

    // ===================== DỮ LIỆU =====================
    enum Zone { Supply, Cave, Hall, Corridor }
    class RoomDef
    {
        public string id, title; public RectInt r; public Zone zone;
        public GameObject go; public Object vcam;
        public int ix0 => r.xMin + 1; public int ix1 => r.xMax - 2;
        public int iy0 => r.yMin + 1; public int iy1 => r.yMax - 2;
        public bool Knight => (zone == Zone.Hall || zone == Zone.Corridor) && id != "F3_R18";
    }

    static Dictionary<string, RoomDef> rooms;
    static int minX, minY, W, H;
    static bool[,] solid, lava, plat, wood, knight;
    static Vector2 cs = Vector2.one;
    static float jumpH = 4f; static int step = 3;
    static readonly List<Vector3> enemyWalls = new List<Vector3>();
    static readonly List<string> log = new List<string>();
    static readonly HashSet<string> spiderRooms = new HashSet<string>();
    // Cầu thang: (x trái, sàn thấp, đi lên sang phải?)
    static readonly List<(int x, int stand, bool up)> stairs = new List<(int, int, bool)>();
    static readonly List<(int a, int b)> stairZones = new List<(int, int)>();
    static bool InStair(int x) => stairZones.Any(z => x >= z.a && x <= z.b);
    /// Ô trống đầu tiên tính từ sàn phòng trở lên (đứng được trên sàn nâng của khu cầu thang)
    static int Stand(int x, int from) { int y = from; while (Solid(x, y) && y < from + 8) y++; return y; }

    static readonly (string room, string prefab, float fx, float fy)[] EnemyList =
    {
        ("F3_R03", "Bat-IdleFly_0", .55f, .35f), ("F3_R03", "Bat-IdleFly_0", .45f, .7f),
        ("F3_R04", "Goblin", .45f, 0), ("F3_R04", "Goblin", .75f, 0),
        ("F3_R05", "Goblin", .9f, 0), ("F3_R05", "Bat-IdleFly_0", .5f, .6f),
        ("F3_R06", "Bat-IdleFly_0", .5f, .4f), ("F3_R06", "Bat-IdleFly_0", .5f, .75f),
        ("F3_R07", "Goblin", .25f, 0), ("F3_R07", "Devil", .45f, 0),
        ("F3_R09", "Goblin", .35f, 0), ("F3_R09", "Devil", .7f, 0), ("F3_R09", "Bat-IdleFly_0", .5f, .6f),
        ("F3_R10", "Bat-IdleFly_0", .45f, .6f),
        ("F3_R11", "Devil", .6f, 0), ("F3_R11", "Goblin", .92f, 0),
        ("F3_R12", "Devil", .85f, 0), ("F3_R12", "Goblin", .6f, 0), ("F3_R12", "Bat-IdleFly_0", .3f, .6f),
        ("F3_R13", "Skeleton", .75f, 0), ("F3_R13", "Spider", .3f, 0),
        ("F3_R14", "Skeleton", .2f, 0), ("F3_R14", "Spider2", .45f, 0), ("F3_R14", "Undead", .6f, 0),
        ("F3_R15", "Undead", .2f, 0), ("F3_R15", "Skeleton", .45f, 0), ("F3_R15", "Spider", .75f, 0),
        ("F3_R17", "Spider2", .5f, 0),
        ("F3_R19", "Spider", .55f, 0), ("F3_R19", "Spider2", .85f, 0),
        ("F3_R21", "Skeleton", .4f, 0), ("F3_R21", "Undead", .75f, 0),
        ("F3_R22", "Undead", .35f, 0), ("F3_R22", "Undead", .7f, 0),
        ("F3_R23", "Skeleton", .2f, 0), ("F3_R23", "Undead", .65f, 0),
        ("F3_R24", "Undead", .4f, 0), ("F3_R24", "Skeleton", .6f, 0), ("F3_R24", "Undead", .8f, 0),
        ("F3_R25", "Undead", .62f, 0),
    };

    // ===================== MENU =====================
    [MenuItem("Tools/Culinaria/Build Third Floor (F3)")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneNameExpected)
        {
            EditorUtility.DisplayDialog("F3 Builder", "Hãy mở ThirdFloorScene và đặt làm scene active trước.", "OK");
            return;
        }
        log.Clear(); enemyWalls.Clear(); spiderRooms.Clear(); stairs.Clear(); stairZones.Clear();
        foreach (var e in EnemyList) if (e.prefab.StartsWith("Spider")) spiderRooms.Add(e.room);

        MeasureJump();
        ReadOldGridCellSize(scene);
        ClearScene(scene);
        DefineRooms();
        BuildGeometry();
        WriteTilemaps(out Transform gridT);
        BuildBackgroundSprites();
        BuildStairs();
        BuildRooms();
        BuildDoorsAndInteract();
        BuildNPCs();
        BuildEnemies();
        BuildBoss();
        BuildEnemyWalls();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add($"Xong: {rooms.Count} phòng. jumpH={jumpH:F2} step={step} cellSize={cs}");
        foreach (var l in log) Debug.Log("[F3] " + l);
    }

    // ===================== ĐO NHẢY =====================
    static void MeasureJump()
    {
        Player p = Object.FindObjectOfType<Player>(true);
        Scene opened = default;
        if (p == null)
        {
            string path = "Assets/Scenes/PersistentGameplay.unity";
            if (System.IO.File.Exists(path))
            {
                opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                p = Object.FindObjectOfType<Player>(true);
            }
        }
        if (p != null)
        {
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            float g = Mathf.Abs(Physics2D.gravity.y) * (rb != null ? rb.gravityScale : 1f);
            if (g > 0.01f) jumpH = p.jumpForce * p.jumpForce / (2f * g);
            log.Add($"Đo nhảy từ Player: jumpForce={p.jumpForce} gravityScale={(rb != null ? rb.gravityScale : 1f)} → cao ~{jumpH:F2} ô");
        }
        else log.Add("Không tìm thấy Player, giả định nhảy cao 4 ô");
        if (opened.IsValid()) EditorSceneManager.CloseScene(opened, true);
        jumpH = Mathf.Clamp(jumpH, 2.5f, 8f);
        step = Mathf.Clamp(Mathf.FloorToInt(jumpH * 0.75f), 2, 4);
    }

    static void ReadOldGridCellSize(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            Grid g = root.GetComponent<Grid>();
            if (g != null) { cs = new Vector2(g.cellSize.x, g.cellSize.y); break; }
        }
    }

    static void ClearScene(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
    }

    // ===================== PHÒNG =====================
    static void R(string id, string title, int x, int y, int w, int h, Zone z)
        => rooms[id] = new RoomDef { id = id, title = title, r = new RectInt(x, y, w, h), zone = z };

    static void DefineRooms()
    {
        rooms = new Dictionary<string, RoomDef>();
        R("F3_R01", "Cổng tiếp tế (lối vào, điểm lưu)", 0, 0, 40, 18, Zone.Supply);
        R("F3_R02", "Chợ thương nhân (hub)", 40, 0, 44, 18, Zone.Supply);
        R("F3_R03", "Giếng nhũ đá A", 4, 18, 24, 38, Zone.Cave);
        R("F3_R04", "Hang phía Tây", -32, 18, 36, 18, Zone.Cave);
        R("F3_R05", "Hố dung nham", 28, 18, 36, 18, Zone.Cave);
        R("F3_R06", "Giếng nhũ đá B", 64, 18, 22, 38, Zone.Cave);
        R("F3_R07", "Phòng trưng nhũ đá", 28, 36, 36, 20, Zone.Cave);
        R("F3_R08", "Khe ẩn (khóa Wall Jump A)", -30, 36, 8, 32, Zone.Cave);
        R("F3_R09", "Tổ dơi", 86, 18, 32, 20, Zone.Cave);
        R("F3_R10", "Vực dung nham (khóa Double Jump A)", 118, 18, 34, 20, Zone.Cave);
        R("F3_R11", "Ngã tư hang", 4, 56, 60, 16, Zone.Cave);
        R("F3_R12", "Hang quỷ", 64, 56, 36, 16, Zone.Cave);
        R("F3_R13", "Cổng hành lang (máy chém)", 4, 72, 44, 16, Zone.Hall);
        R("F3_R14", "Sảnh kỵ sĩ I", 48, 72, 48, 16, Zone.Hall);
        R("F3_R15", "Sảnh kỵ sĩ II", 96, 72, 48, 16, Zone.Hall);
        R("F3_R16", "Kho giáp trên cao (khóa Wall Jump B)", 112, 88, 8, 30, Zone.Hall);
        R("F3_R17", "Tiền sảnh boss (điểm lưu)", 144, 72, 24, 22, Zone.Hall);
        R("F3_R19", "Hầm mộ nhện (khóa Double Jump B)", -24, 72, 28, 20, Zone.Hall);
        string[] cn = { "Hành lang đá I - Cổng tượng", "Hành lang đá II - Dãy cờ", "Hành lang đá III - Gian nến",
                        "Hành lang đá IV - Máy chém", "Hành lang đá V - Cửa đại sảnh" };
        for (int i = 0; i < CorridorRooms; i++)
            R($"F3_R{21 + i}", cn[i % cn.Length], 168 + i * CorridorRoomW, 72, CorridorRoomW, 18, Zone.Corridor);
        R("F3_R18", "Phòng boss Demon Slime", 168 + BO, 72, 44, 24, Zone.Corridor);
        R("F3_R20", "Gác thượng & lối lên tầng 4 (khóa Wall Jump C)", 202 + BO, 96, 10, 26, Zone.Hall);

        int x0 = rooms.Values.Min(r => r.r.xMin) - 4, y0 = rooms.Values.Min(r => r.r.yMin) - 4;
        int x1 = rooms.Values.Max(r => r.r.xMax) + 4, y1 = rooms.Values.Max(r => r.r.yMax) + 4;
        minX = x0; minY = y0; W = x1 - x0; H = y1 - y0;
        solid = new bool[W, H]; lava = new bool[W, H]; plat = new bool[W, H]; wood = new bool[W, H]; knight = new bool[W, H];
        foreach (var r in rooms.Values.Where(r => r.Knight))
            for (int x = r.r.xMin; x < r.r.xMax; x++) for (int y = r.r.yMin; y < r.r.yMax; y++)
                if (In(x, y)) knight[x - minX, y - minY] = true;
    }

    // ===================== HÌNH KHỐI =====================
    static bool In(int x, int y) => x >= minX && y >= minY && x < minX + W && y < minY + H;
    static void Set(int x, int y, bool v) { if (In(x, y)) solid[x - minX, y - minY] = v; }
    static bool Solid(int x, int y) => !In(x, y) || solid[x - minX, y - minY];
    static bool Lava(int x, int y) => In(x, y) && lava[x - minX, y - minY];
    static bool Plat(int x, int y) => In(x, y) && (plat[x - minX, y - minY] || wood[x - minX, y - minY]);
    static bool Wood(int x, int y) => In(x, y) && wood[x - minX, y - minY];
    static bool Knight(int x, int y) => In(x, y) && knight[x - minX, y - minY];
    static void FillPlat(int x, int y, int w, int h) { for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) if (In(x + i, y + j)) plat[x + i - minX, y + j - minY] = true; }
    static void FillWood(int x, int y, int w) { for (int i = 0; i < w; i++) if (In(x + i, y)) wood[x + i - minX, y - minY] = true; }
    static void Fill(int x, int y, int w, int h) { for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) Set(x + i, y + j, true); }
    static void Carve(int x, int y, int w, int h) { for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) Set(x + i, y + j, false); }

    static void LavaPit(int x, int yStand, int w, int depth)
    {
        for (int i = 0; i < w; i++) for (int j = 1; j <= depth; j++)
        {
            int cx = x + i, cy = yStand - j;
            if (!In(cx, cy)) continue;
            solid[cx - minX, cy - minY] = false;
            lava[cx - minX, cy - minY] = true;
        }
        Fill(x, yStand - depth - 1, w, 1);
    }

    static void HDoor(int bx, int yStand) => Carve(bx - 1, yStand, 2, 4);

    /// Bỏ hẳn vách ngăn giữa 2 phòng cùng chiều cao → thành 1 hành lang liền, không còn "cửa".
    static void OpenWall(RoomDef a) => Carve(a.r.xMax - 1, a.iy0, 2, a.r.height - 2);

    static void Climb(int x0, int yStand, int by, RoomDef above, bool endLeft)
    {
        int x1 = x0 + ClimbColumnWidth;
        var stands = new List<int>();
        int s = yStand;
        while (by + 1 - s > step)
        {
            int ns = Mathf.Min(s + step, by - 1);
            if (ns <= s) break;
            s = ns; stands.Add(s);
        }
        int n = stands.Count;
        for (int k = 0; k < n; k++)
        {
            bool left = ((n - 1 - k) % 2 == 0) ? endLeft : !endLeft;
            int px = left ? x0 : x1 - PlatformWidth;
            FillPlat(px, stands[k] - 1, PlatformWidth, 1);
        }
        int h0 = n == 0 ? x0 + 3 : (endLeft ? x0 + 1 : x1 - 6);
        int h1 = h0 + 5;
        int ax0 = above.ix0, ax1 = above.ix1 + 1;
        h0 = Mathf.Max(h0, ax0); h1 = Mathf.Min(h1, ax1);
        if (h1 - h0 < 4) { h0 = Mathf.Clamp(h0, ax0, ax1 - 4); h1 = h0 + 4; }
        Carve(h0, by - 1, h1 - h0, 2);
        enemyWalls.Add(new Vector3(h0 - 0.5f, by + 1, 2));
        enemyWalls.Add(new Vector3(h1 + 0.5f, by + 1, 2));
    }

    /// Platform gỗ (Dungeon2) rải dọc hành lang: tầng 1 cao 3 ô, xen kẽ tầng 2 cao thêm 3 ô.
    static void WoodPlatforms(int x0, int x1, int stand, params (int a, int b)[] skip)
    {
        int lo = stand + 2, hi = stand + 5;   // hàng va chạm (đứng ở +1)
        int k = 0;
        for (int x = x0; x + 5 <= x1; x += 13, k++)
        {
            if (skip.Any(s => x + 5 >= s.a && x <= s.b)) continue;
            FillWood(x, lo, 5);
            if (k % 2 == 1 && !skip.Any(s => x + 9 >= s.a && x + 4 <= s.b)) FillWood(x + 4, hi, 5);
        }
    }

    static int ledgeR10Top, ledgeR19Stand;

    static void BuildGeometry()
    {
        Fill(minX, minY, W, H);
        foreach (var r in rooms.Values) Carve(r.ix0, r.iy0, r.r.width - 2, r.r.height - 2);

        var Rm = rooms;
        HDoor(40, 1);      // R01-R02
        HDoor(4, 19);      // R04-R03
        HDoor(28, 19);     // R03-R05
        HDoor(64, 19);     // R05-R06
        HDoor(86, 19);     // R06-R09
        HDoor(118, 19);    // R09-R10
        HDoor(64, 57);     // R11-R12
        HDoor(4, 73);      // R19-R13
        HDoor(144, 73);    // R15-R17
        HDoor(168, 73);    // R17-R21
        HDoor(168 + BO, 73); // R25-R18 (cổng boss)

        // Hành lang liền mạch: bỏ vách ngăn (bớt cửa)
        OpenWall(Rm["F3_R13"]); OpenWall(Rm["F3_R14"]);
        for (int i = 0; i < CorridorRooms - 1; i++) OpenWall(Rm[$"F3_R{21 + i}"]);

        Climb(6, 1, 18, Rm["F3_R03"], true);
        Climb(6, 19, 56, Rm["F3_R11"], false);
        Climb(70, 19, 56, Rm["F3_R12"], true);
        Climb(50, 37, 56, Rm["F3_R11"], true);
        Climb(22, 57, 72, Rm["F3_R13"], true);
        Climb(84, 57, 72, Rm["F3_R14"], false);
        Climb(-30, 19, 36, Rm["F3_R08"], true);
        Climb(112, 73, 88, Rm["F3_R16"], true);

        Fill(-29, 60, 3, 1);
        Fill(113, 110, 3, 1);
        Carve(203 + BO, 94, 8, 3);
        Fill(203 + BO, 114, 4, 1);

        LavaPit(36, 19, 21, 1);
        int sr = 19 + step - 1;
        FillPlat(39, sr, 3, 1); FillPlat(45, sr + 1, 3, 1); FillPlat(51, sr, 3, 1);

        LavaPit(124, 19, 21, 2);
        FillPlat(128, sr, 3, 1); FillPlat(135, sr, 3, 1);
        int L = Mathf.CeilToInt(jumpH) + 1 + step;
        Fill(145, 19, 6, L);
        ledgeR10Top = 19 + L;

        int D = Mathf.CeilToInt(jumpH) + 1;
        Fill(-21, 73 + D - 1, 6, 1);
        ledgeR19Stand = 73 + D;

        // Khu cầu thang KnightDungeon trong hành lang đá: lên 3 ô → sàn nâng → xuống (2 lần: R22-R23, R24-R25)
        foreach (var (up, down) in new[] { (214, 262), (294, 342) })
        {
            stairs.Add((up, 73, true)); stairs.Add((down, 73, false));
            int topX = up + Mathf.FloorToInt(StairW);          // ô đầu tiên của sàn nâng
            Fill(topX, 73, down - topX, StairRise);
            stairZones.Add((up - 1, up + 6)); stairZones.Add((down - 1, down + 6));
        }

        // Platform gỗ dọc 2 hành lang (tránh cột leo R16, miệng hố leo, và khu cầu thang)
        WoodPlatforms(10, 140, 73, (20, 30), (82, 96), (108, 126));
        WoodPlatforms(176, 208, 73);
        WoodPlatforms(223, 258, 76);
        WoodPlatforms(270, 290, 73);
        WoodPlatforms(303, 338, 76);

        log.Add($"Gờ Double Jump: R10 cao {L} ô từ sàn (đứng y={ledgeR10Top}), R19 cao {D} ô");
    }

    // ===================== TILEMAP =====================
    static TileBase T(string path)
    {
        var t = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        if (t == null) log.Add("THIẾU tile: " + path);
        return t;
    }
    static TileBase K(string n) => T(KD + n + ".asset");
    static TileBase DG(int n) => T(D2 + n + ".asset");

    static Tilemap NewTilemap(Transform grid, string name, int order, Color tint, int layer = 0, Material mat = null)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid, false);
        go.layer = layer;
        var tm = go.GetComponent<Tilemap>(); tm.color = tint;
        var tr = go.GetComponent<TilemapRenderer>(); tr.sortingOrder = order;
        if (mat != null) tr.sharedMaterial = mat;
        return tm;
    }

    static void AddSolidCollider(Tilemap tm)
    {
        var tc = tm.gameObject.AddComponent<TilemapCollider2D>();
        var rb = tm.gameObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Static;
        tm.gameObject.AddComponent<CompositeCollider2D>();
#pragma warning disable 0618
        tc.usedByComposite = true;
#pragma warning restore 0618
    }

static Material BrightMaterial(string path, float k)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = new Color(k, k, k, 1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static int Hash(int x, int y) => Mathf.Abs((x * 73856093) ^ (y * 19349663));

    static void WriteTilemaps(out Transform gridT)
    {
        var gridGo = new GameObject("Grid", typeof(Grid));
        gridGo.GetComponent<Grid>().cellSize = new Vector3(cs.x, cs.y, 0);
        gridT = gridGo.transform;
        var bright = BrightMaterial(BrightMatPath, KnightBright);
        var bgMat = BrightMaterial(BgMatPath, KnightBgBright);

        TileBase solidT = T(TM + "dungeontilesetfull 1_33.asset");
        TileBase topT = T(TM + "dungeontilesetfull 1_1.asset");
        TileBase lavaTop = T(TM + "dungeontilesetfull 1_298.asset"), lavaTop2 = T(TM + "dungeontilesetfull 1_299.asset");
        TileBase lavaDeep = T(TM + "dungeontilesetfull 1_300.asset"), lavaDeep2 = T(TM + "dungeontilesetfull 1_301.asset");
        TileBase[] kBricks = { K("Bricks1"), K("Bricks2"), K("Bricks3"), K("Bricks4") };
        TileBase[] kFloor = { K("Floor1"), K("Floor2"), K("Floor3"), K("Floor4") };
        TileBase[] kCeil = { K("Sealing1"), K("Sealing2"), K("Sealing3"), K("Sealing4") };
        TileBase[] kBg = { K("Bricks1BG"), K("Bricks2BG"), K("Bricks3BG"), K("Bricks4BG") };

        var kBgTm = NewTilemap(gridT, "F3_BG_KnightDungeon", -12, Color.white, 0, bgMat);
        var ground = NewTilemap(gridT, "F3_Ground", 0, GroundTint, 6);
        var kGround = NewTilemap(gridT, "F3_Ground_KnightDungeon", 0, Color.white, 6, bright);
        var decor = NewTilemap(gridT, "F3_Decor", 1, DecorTint);
        var lavaTm = NewTilemap(gridT, "F3_DeadZone_Lava", 2, Color.white);
        lavaTm.gameObject.tag = "DeadZone";

        // Nền KnightDungeon cho hành lang (trừ phòng cổng giữ hall.png)
        var bp = new List<Vector3Int>(); var bt = new List<TileBase>();
        foreach (var r in rooms.Values.Where(r => r.Knight))
            for (int x = r.r.xMin & ~1; x < r.r.xMax; x += 2) for (int y = r.r.yMin & ~1; y < r.r.yMax; y += 2)   // tile nền 2x2 ô: đặt cách 1 ô, giữ kích thước gốc
            { var p = new Vector3Int(x, y, 0); if (bp.Contains(p)) continue; bp.Add(p); bt.Add(kBg[Hash(x, y) % kBg.Length]); }
        kBgTm.SetTiles(bp.ToArray(), bt.ToArray());
        var bgShift = Matrix4x4.Translate(new Vector3(0.5f * cs.x, 0.5f * cs.y, 0));
        foreach (var p in bp) { kBgTm.SetTileFlags(p, TileFlags.None); kBgTm.SetTransformMatrix(p, bgShift); }

        var gp = new List<Vector3Int>(); var gt = new List<TileBase>();
        var kp = new List<Vector3Int>(); var kt = new List<TileBase>();
        var lp = new List<Vector3Int>(); var lt = new List<TileBase>();
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
        {
            int x = i + minX, y = j + minY;
            if (solid[i, j])
            {
                bool openAbove = !Solid(x, y + 1) && !Lava(x, y + 1);
                bool openBelow = !Solid(x, y - 1) && !Lava(x, y - 1);
                if (knight[i, j])
                {
                    kp.Add(new Vector3Int(x, y, 0));
                    int h = Hash(x, y);
                    kt.Add(openAbove ? kFloor[h % 4] : openBelow ? kCeil[h % 4] : kBricks[h % 4]);
                }
                else
                {
                    gp.Add(new Vector3Int(x, y, 0));
                    gt.Add(UseTopTile && openAbove && topT != null ? topT : solidT);
                }
            }
            if (lava[i, j])
            {
                lp.Add(new Vector3Int(x, y, 0));
                bool surface = !Lava(x, y + 1); bool odd = (x & 1) == 1;
                lt.Add(surface ? (odd && lavaTop2 != null ? lavaTop2 : lavaTop) : (odd && lavaDeep2 != null ? lavaDeep2 : lavaDeep ?? lavaTop));
            }
        }
        ground.SetTiles(gp.ToArray(), gt.ToArray());
        kGround.SetTiles(kp.ToArray(), kt.ToArray());
        // Tile KnightDungeon rộng 2x2 ô → thu về 1 ô để không lấn vào lòng phòng (mặt sàn khớp va chạm)
        var half = Matrix4x4.Scale(new Vector3(0.5f, 0.5f, 1f));
        foreach (var p in kp) { kGround.SetTileFlags(p, TileFlags.None); kGround.SetTransformMatrix(p, half); }
        lavaTm.SetTiles(lp.ToArray(), lt.ToArray());
        AddSolidCollider(ground);
        AddSolidCollider(kGround);
        var lc = lavaTm.gameObject.AddComponent<TilemapCollider2D>(); lc.isTrigger = true;

        // Bậc một chiều: đá (1_65, lật dọc) + gỗ Dungeon2 (D77 đầu trái, D78 thân, D79 đầu phải)
        var platTm = NewTilemap(gridT, "F3_Platform_OneWay", 0, Color.white, 6);
        TileBase stoneT = T(TM + "dungeontilesetfull 1_65.asset");
        TileBase wL = DG(77), wM = DG(78), wR = DG(79);
        int nStone = 0, nWood = 0;
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
        {
            if (solid[i, j]) continue;
            int x = i + minX, y = j + minY; var p = new Vector3Int(x, y, 0);
            bool woodLike = wood[i, j] || (plat[i, j] && knight[i, j]);   // bậc trong hành lang cũng dùng gỗ
            if (woodLike)
            {
                bool l = Wood(x - 1, y) || (Plat(x - 1, y) && Knight(x - 1, y)), rr = Wood(x + 1, y) || (Plat(x + 1, y) && Knight(x + 1, y));
                platTm.SetTile(p, !l ? wL : !rr ? wR : wM); nWood++;
            }
            else if (plat[i, j])
            {
                platTm.SetTile(p, stoneT); nStone++;
                platTm.SetColor(p, GroundTint);
                if (FlipPlatformTile) { platTm.SetTileFlags(p, TileFlags.None); platTm.SetColor(p, GroundTint); platTm.SetTransformMatrix(p, Matrix4x4.Scale(new Vector3(1, -1, 1))); }
            }
        }
        var ptc = platTm.gameObject.AddComponent<TilemapCollider2D>();
        var prb = platTm.gameObject.AddComponent<Rigidbody2D>(); prb.bodyType = RigidbodyType2D.Static;
        var pcc = platTm.gameObject.AddComponent<CompositeCollider2D>();
#pragma warning disable 0618
        ptc.usedByComposite = true;
#pragma warning restore 0618
        pcc.usedByEffector = true;
        var eff = platTm.gameObject.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true; eff.surfaceArc = 160f; eff.useSideFriction = false;
        log.Add($"Bậc một chiều: đá {nStone} ô, gỗ Dungeon2 {nWood} ô");

        Decorate(decor);
        log.Add($"Tile: đất {gp.Count}, KnightDungeon {kp.Count}, dung nham {lp.Count}");
    }

    // ===================== ẢNH NỀN =====================
    static void Cover(Transform parent, string name, Sprite s, RectInt area, int order, bool mirrorAlternate, bool startFlipped, int maxCount = 0)
    {
        if (s == null) { log.Add("THIẾU ảnh nền cho " + name); return; }
        var g = new GameObject(name); g.transform.SetParent(parent);
        float areaW = area.width * cs.x, areaH = area.height * cs.y;
        float k = areaH / s.bounds.size.y;
        float w = s.bounds.size.x * k;
        int n = Mathf.Max(1, Mathf.CeilToInt(areaW / w - 0.05f));
        float startX = area.xMin * cs.x;
        if (maxCount > 0 && n > maxCount) { n = maxCount; startX = area.xMin * cs.x + (areaW - w * n) / 2f; }   // 1 bức tranh, căn giữa phòng
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject($"{name}_{i}");
            go.transform.SetParent(g.transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order;
            sr.flipX = startFlipped ^ (mirrorAlternate && (i % 2 == 1));
            go.transform.localScale = new Vector3(k, k, 1);
            Vector3 center = new Vector3(startX + w * (i + 0.5f), area.yMin * cs.y + areaH / 2f, 1f);
            Vector3 c = s.bounds.center * k; if (sr.flipX) c.x = -c.x;
            go.transform.position = center - new Vector3(c.x, c.y, 0);
        }
    }

    static void BuildBackgroundSprites()
    {
        var parent = Group("______BackGround______").transform;
        var cave = AssetDatabase.LoadAssetAtPath<Sprite>(CaveBgPath);
        var hall = AssetDatabase.LoadAssetAtPath<Sprite>(HallBgPath);
        var boss = AssetDatabase.LoadAssetAtPath<Sprite>(BossBgPath);
        foreach (var r in rooms.Values.Where(r => r.zone == Zone.Cave || r.zone == Zone.Supply))
            Cover(parent, "BG_" + r.id, cave, r.r, -20, true, false);
        // Hành lang tượng đá: 2 bức hall.png lật đối xứng nối liền (cổng ở 2 đầu), cao bằng lòng hành lang, kéo sang đầu R22
        var g = rooms[HallGateRoom];
        Cover(parent, "BG_" + HallGateRoom, hall, new RectInt(g.ix0, g.iy0, 56, g.r.height - 2), -11, true, true);
        Cover(parent, "BG_F3_R18", boss, rooms["F3_R18"].r, -20, false, false);
    }

/// Cầu thang KnightDungeon (LEFT_End-End đi lên / RIGHT_End-End đi xuống): sprite + EdgeCollider2D dọc mặt bậc.
    static void BuildStairs()
    {
        var parent = Group("______Stairs_____").transform;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(BrightMatPath);
        var upT = AssetDatabase.LoadAssetAtPath<Tile>(KD + "LEFT_End-End.asset");
        var downT = AssetDatabase.LoadAssetAtPath<Tile>(KD + "RIGHT_End-End.asset");
        int ground = LayerMask.NameToLayer("Ground");
        int i = 0;
        foreach (var s in stairs)
        {
            var t = s.up ? upT : downT;
            if (t == null) { log.Add("THIẾU tile cầu thang KnightDungeon"); continue; }
            var go = new GameObject($"Stair_{(s.up ? "Up" : "Down")}_{++i}");
            go.transform.SetParent(parent);
            go.layer = ground;
            go.transform.position = Wpos(s.x, s.stand);

            // Sprite: đáy sprite chạm sàn, mép trái tại s.x
            var vis = new GameObject("Sprite"); vis.transform.SetParent(go.transform, false);
            var sr = vis.AddComponent<SpriteRenderer>(); sr.sprite = t.sprite; sr.sortingOrder = 0;
            if (mat != null) sr.sharedMaterial = mat;
            var b = t.sprite.bounds;
            vis.transform.localPosition = new Vector3(-b.min.x, -b.min.y + 0.1f, 0);   // nhích 0.1 để mép bậc khớp sàn nâng

            // Va chạm: mặt bậc đi từ sàn thấp lên sàn nâng (3 ô) trên đúng bề rộng sprite
            var edge = go.AddComponent<EdgeCollider2D>();
            float w = b.size.x;
            edge.points = s.up
                ? new[] { new Vector2(-0.3f, 0f), new Vector2(0f, 0f), new Vector2(w, StairRise), new Vector2(w + 0.6f, StairRise) }
                : new[] { new Vector2(-0.6f, StairRise), new Vector2(0f, StairRise), new Vector2(w, 0f), new Vector2(w + 0.3f, 0f) };

            // Quái quay đầu ở chân và đỉnh cầu thang (không đi lên dốc)
            enemyWalls.Add(new Vector3(s.x - 0.8f, s.stand, 3));
            enemyWalls.Add(new Vector3(s.x + w + 0.8f, s.stand, 3));
            enemyWalls.Add(new Vector3(s.up ? s.x + w + 0.8f : s.x - 0.8f, s.stand + StairRise, 3));
        }
        log.Add($"Cầu thang KnightDungeon: {stairs.Count} mảnh");
    }


    // ===================== TRANG TRÍ =====================
    static void PutAligned(Tilemap tm, int x, int y, TileBase t, int v, int h, bool flipX = false)
    {
        if (t == null) return;
        var p = new Vector3Int(x, y, 0);
        tm.SetTile(p, t);
        tm.SetTileFlags(p, TileFlags.None);
        Sprite s = (t as Tile) != null ? ((Tile)t).sprite : null;
        if (s == null) return;
        Bounds b = s.bounds;
        float minX = flipX ? -b.max.x : b.min.x, maxX = flipX ? -b.min.x : b.max.x;
        float dy = v < 0 ? (-0.5f * cs.y - b.min.y) : v > 0 ? (0.5f * cs.y - b.max.y) : 0f;
        float dx = h < 0 ? (-0.5f * cs.x - minX) : h > 0 ? (0.5f * cs.x - maxX) : 0f;
        tm.SetTransformMatrix(p, Matrix4x4.TRS(new Vector3(dx, dy, 0), Quaternion.identity, new Vector3(flipX ? -1 : 1, 1, 1)));
    }

    static bool FreeCell(int x, int y) => !Solid(x, y) && !Lava(x, y) && !Plat(x, y);

    static void Decorate(Tilemap decor)
    {
        var rnd = new System.Random(3303);
        TileBase torch = K("Torch"), candle = K("Candlestick"), web = K("Web1");
        TileBase guillotine = K("AxeChainLong"), chandelier = K("Chandelier"), lamp = DG(115);
        TileBase[] bones = { DG(124), DG(125), DG(126) };                       // xương Dungeon2
        TileBase[] supplyProps = { DG(84), DG(94), DG(117), DG(118), DG(119) }; // thùng, hũ Dungeon2
        TileBase[] wallHang = { K("Flag1"), K("Flag2"), K("Shield1"), K("Flag3"), K("Shield2"), K("Flag4") };
        int nFloor = 0, nTorch = 0, nCandle = 0, nWeb = 0, nCeil = 0, nWall = 0;
        var used = new HashSet<Vector3Int>();
        bool Mark(int x, int y) => used.Add(new Vector3Int(x, y, 0));
        bool OnFloor(int x, int y) => !InStair(x) && Solid(x, y - 1) && FreeCell(x, y) && FreeCell(x, y + 1);

        foreach (var r in rooms.Values)
        {
            bool boss = r.id == "F3_R18";
            bool gate = r.id == HallGateRoom;
            int stand = r.iy0;
            if (boss) continue;

            // 1) Đồ trên sàn (thưa)
            if (r.r.width > 12)
                for (int x = r.ix0 + 4; x <= r.ix1 - 4; x += rnd.Next(10, 17))
                {
                    int fy = Stand(x, stand);
                    if (rnd.NextDouble() > 0.6 || !OnFloor(x, fy) || !Mark(x, fy)) continue;
                    var list = r.zone == Zone.Supply ? supplyProps : bones;
                    PutAligned(decor, x, fy, list[rnd.Next(list.Length)], -1, 0); nFloor++;
                }

            if (r.Knight)
            {
                // 2a) Nến đứng trên sàn mỗi ~10 ô (chỉ hành lang)
                for (int x = r.ix0 + 5; x <= r.ix1 - 5; x += 10)
                    { int fy = Stand(x, stand); if (OnFloor(x, fy) && Mark(x, fy)) { PutAligned(decor, x, fy, candle, -1, 0); nCandle++; } }
                // 2b) Cờ/khiên treo trên vách gạch nền (không đặt ở phòng cổng có tranh hall.png)
                if (!gate && r.r.height >= 10)
                    for (int x = r.ix0 + 9; x <= r.ix1 - 9; x += 16)
                    {
                        int y = Stand(x, stand) + 5;
                        bool onHallPicture = r.zone == Zone.Corridor && x >= rooms[HallGateRoom].ix0 && x < rooms[HallGateRoom].ix0 + 57;
                        if (!InStair(x) && !onHallPicture && FreeCell(x, y) && FreeCell(x, y - 1) && FreeCell(x, y + 1) && Mark(x, y))
                        { PutAligned(decor, x, y, wallHang[rnd.Next(wallHang.Length)], 0, 0); nWall++; }
                    }
            }
            else if (r.r.height >= 10)
            {
                // 2c) Đuốc gắn sát vách (mê cung + trạm tiếp tế)
                int ty = stand + 2;
                if (Solid(r.ix0 - 1, ty) && FreeCell(r.ix0, ty) && Mark(r.ix0, ty)) { PutAligned(decor, r.ix0, ty, torch, 0, -1); nTorch++; }
                if (Solid(r.ix1 + 1, ty) && FreeCell(r.ix1, ty) && Mark(r.ix1, ty)) { PutAligned(decor, r.ix1, ty, torch, 0, 1, true); nTorch++; }
            }

            // 3) Mạng nhện: chỉ phòng có nhện, ở góc trần
            if (spiderRooms.Contains(r.id))
            {
                int cy = r.iy1;
                if (Solid(r.ix0 - 1, cy) && Solid(r.ix0, cy + 1) && FreeCell(r.ix0, cy) && Mark(r.ix0, cy)) { PutAligned(decor, r.ix0, cy, web, 1, -1); nWeb++; }
                if (Solid(r.ix1 + 1, cy) && Solid(r.ix1, cy + 1) && FreeCell(r.ix1, cy) && Mark(r.ix1, cy)) { PutAligned(decor, r.ix1, cy, web, 1, 1, true); nWeb++; }
            }

            // 4) Trần
            if (r.zone == Zone.Supply)
            {
                foreach (int x in new[] { r.ix0 + r.r.width / 3, r.ix0 + 2 * r.r.width / 3 })
                    if (Solid(x, r.iy1 + 1) && FreeCell(x, r.iy1) && Mark(x, r.iy1)) { PutAligned(decor, x, r.iy1, lamp, 1, 0); nCeil++; }
            }
            else if (r.Knight && r.r.width >= 20)
            {
                int x = (r.ix0 + r.ix1) / 2 + rnd.Next(-4, 5);
                var t = (r.id == "F3_R13" || r.id == "F3_R24") ? guillotine : chandelier;   // máy chém ở cổng + phòng IV
                if (Solid(x, r.iy1 + 1) && FreeCell(x, r.iy1) && FreeCell(x, r.iy1 - 1) && Mark(x, r.iy1)) { PutAligned(decor, x, r.iy1, t, 1, 0); nCeil++; }
            }
        }
        log.Add($"Trang trí: sàn {nFloor}, đuốc {nTorch}, nến {nCandle}, cờ/khiên {nWall}, mạng nhện {nWeb}, trần {nCeil}");
    }

    // ===================== ROOM (Module 5) =====================
    static Vector3 Wpos(float x, float y) => new Vector3(x * cs.x, y * cs.y, 0);
    static GameObject Group(string name) => new GameObject(name);

    static void BuildRooms()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Room.prefab");
        var parent = Group("______Room_____").transform;
        foreach (var r in rooms.Values.OrderBy(r => r.id))
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = r.id;
            go.layer = LayerMask.NameToLayer("Room");
            go.transform.position = Wpos(r.r.xMin, r.r.yMin);
            var poly = go.GetComponent<PolygonCollider2D>();
            float w = r.r.width * cs.x, h = r.r.height * cs.y;
            poly.pathCount = 1;
            poly.SetPath(0, new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h) });
            poly.offset = Vector2.zero;

            var room = go.GetComponent<global::Room>();
            var so = new SerializedObject(room);
            so.FindProperty("roomId").stringValue = r.id;
            so.ApplyModifiedPropertiesWithoutUndo();
            r.go = go; r.vcam = so.FindProperty("vcam").objectReferenceValue;

            if (r.vcam is Component vc)
            {
                foreach (var c in vc.GetComponents<Component>())
                {
                    if (c == null || c.GetType().Name != "CinemachineConfiner2D") continue;
                    var cso = new SerializedObject(c);
                    var p = cso.FindProperty("m_BoundingShape2D");
                    if (p != null) { p.objectReferenceValue = poly; cso.ApplyModifiedPropertiesWithoutUndo(); }
                    var m = c.GetType().GetMethod("InvalidateCache");
                    if (m != null) m.Invoke(c, null);
                }
                vc.gameObject.name = "VCam_" + r.id;
                vc.transform.position = new Vector3(go.transform.position.x + w / 2, go.transform.position.y + h / 2, -10);
            }
        }
    }

    // ===================== CỬA, RƯƠNG, ĐIỂM LƯU =====================
    static GameObject Inst(string path, Transform parent, Vector3 pos, string name = null)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (pf == null) { log.Add("THIẾU prefab: " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = pos;
        if (name != null) go.name = name;
        return go;
    }

    static GameObject doorUp;

    static GameObject MakeDoor(Transform parent, string doorId, Vector3 pos, string targetScenePath, string targetDoorId, int enterDir)
    {
        var go = Inst("Assets/Prefab/SceneDoor.prefab", parent, pos, doorId);
        go.transform.localScale = new Vector3(0.5f, 0.5f, 1);
        var door = go.GetComponent<SceneDoor>();
        var so = new SerializedObject(door);
        so.FindProperty("doorId").stringValue = doorId;
        so.FindProperty("targetDoorId").stringValue = targetDoorId;
        so.FindProperty("requireInteract").boolValue = true;
        so.FindProperty("enterDirection").enumValueIndex = enterDir;
        var ts = so.FindProperty("targetScene");
        var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(targetScenePath);
        var pa = ts.FindPropertyRelative("sceneAsset"); if (pa != null) pa.objectReferenceValue = asset;
        var pn = ts.FindPropertyRelative("sceneName"); if (pn != null) pn.stringValue = asset != null ? asset.name : "";
        Transform spawn = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        if (spawn != null)
        {
            spawn.name = "SpawnPoint";
            spawn.position = pos + new Vector3((enterDir == 1 ? 2.5f : -2.5f) * cs.x, 0, 0);
            so.FindProperty("spawnPoint").objectReferenceValue = spawn;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    static void MakeChest(Transform parent, string id, float x, float yStand, int coins)
    {
        var go = Inst("Assets/Prefab/Chest.prefab", parent, Wpos(x, yStand + 0.5f), id);
        if (go == null) return;
        var so = new SerializedObject(go.GetComponent<Chest>());
        so.FindProperty("chestId").stringValue = id;
        so.FindProperty("coinAmount").intValue = coins;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void MakeCheckpoint(Transform parent, string id, float x, float yStand, RoomDef room)
    {
        var go = new GameObject(id);
        go.transform.SetParent(parent);
        go.transform.position = Wpos(x, yStand + 1.5f);
        var bc = go.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = new Vector2(2 * cs.x, 3 * cs.y);
        var cp = go.AddComponent<CheckPoint>();
        var so = new SerializedObject(cp);
        so.FindProperty("checkpointId").stringValue = id;
        so.FindProperty("areaCamera").objectReferenceValue = room.vcam;
        so.ApplyModifiedPropertiesWithoutUndo();
        Inst("Assets/Prefab/CampFire.prefab", go.transform, Wpos(x, yStand + 0.5f), "CampFire");
    }

    static void BuildDoorsAndInteract()
    {
        var doors = Group("______Door_____").transform;
        MakeDoor(doors, "F3_Down", Wpos(3.5f, 2.5f), "Assets/Scenes/SecondFloorScene.unity", "F2_Down", 1);
        doorUp = MakeDoor(doors, "F3_Up", Wpos(206f + BO, 116.5f), "Assets/Scenes/FourthFloorScene.unity", "F4_Down", 0);
        doorUp.SetActive(false);

        var sp = new GameObject("ThirdFloorSpawnPoint"); sp.tag = "SpawnPoint";
        sp.transform.SetParent(doors); sp.transform.position = Wpos(6.5f, 1.5f);

        var it = Group("______Interact_____").transform;
        MakeCheckpoint(it, "F3_CP01", 12.5f, 1, rooms["F3_R01"]);
        MakeCheckpoint(it, "F3_CP02", 155.5f, 73, rooms["F3_R17"]);
        var last = rooms[$"F3_R{20 + CorridorRooms}"];
        MakeCheckpoint(it, "F3_CP03", last.r.xMin + last.r.width / 2f + 3, 73, last);

        MakeChest(it, "F3_Chest01", 36.5f, 37, 300);
        MakeChest(it, "F3_Chest02", -27.5f, 61, 800);
        MakeChest(it, "F3_Chest03", 149.5f, ledgeR10Top, 700);
        MakeChest(it, "F3_Chest04", 114.5f, 111, 800);
        MakeChest(it, "F3_Chest05", -18.5f, ledgeR19Stand, 700);
        MakeChest(it, "F3_Chest06", 203.5f + BO, 115, 1000);

        var safe = new GameObject("F3_SafeZone_TODO");
        safe.transform.SetParent(it);
        var r1 = rooms["F3_R01"].r; var r2 = rooms["F3_R02"].r;
        safe.transform.position = Wpos((r1.xMin + r2.xMax) / 2f, (r1.yMin + r1.yMax) / 2f);
        var sb = safe.AddComponent<BoxCollider2D>(); sb.isTrigger = true;
        sb.size = new Vector2((r2.xMax - r1.xMin - 2) * cs.x, (r1.height - 2) * cs.y);
    }

    static void BuildNPCs()
    {
        var npc = Group("______NPC_____").transform;
        string[] names = { "Thương nhân Muối", "Bà bán Nấm Hang", "Gã đổi Xương" };
        float[] xs = { 52.5f, 62.5f, 72.5f };
        for (int i = 0; i < 3; i++)
        {
            var go = Inst("Assets/Prefab/NPC/ShopNpc.prefab", npc, Wpos(xs[i], 2f), "Merchant_" + (i + 1));
            if (go != null) go.GetComponent<ShopNPC>().shopkeeperName = names[i];
        }
    }

    // ===================== QUÁI =====================
    static void BuildEnemies()
    {
        var parent = Group("______Enemy_____").transform;
        const string E = "Assets/Prefab/Enemy/";
        var groundRooms = new HashSet<string>();
        int i = 0;
        foreach (var e in EnemyList)
        {
            var r = rooms[e.room];
            float x = r.r.xMin + e.fx * r.r.width;
            float y = e.fy > 0 ? r.r.yMin + e.fy * r.r.height : Stand(Mathf.FloorToInt(x), r.iy0) + 1.2f;   // đứng trên sàn nâng nếu có
            Inst(E + e.prefab + ".prefab", parent, Wpos(x, y), $"{e.prefab}_{e.room}_{++i}");
            if (e.fy <= 0) groundRooms.Add(e.room);
        }
        foreach (var id in groundRooms)
        {
            var r = rooms[id];
            enemyWalls.Add(new Vector3(r.ix0 + 0.5f, Stand(r.ix0, r.iy0), 3));
            enemyWalls.Add(new Vector3(r.ix1 + 0.5f, Stand(r.ix1, r.iy0), 3));
        }
        enemyWalls.Add(new Vector3(35.5f, 19, 3)); enemyWalls.Add(new Vector3(57.5f, 19, 3));
        log.Add($"Quái: {EnemyList.Length} con");
    }

    static void BuildEnemyWalls()
    {
        var parent = Group("______EnemyWall_____").transform;
        int i = 0;
        foreach (var w in enemyWalls)
        {
            var go = new GameObject("EnemyWall_" + (++i)); go.tag = "EnemyWall";
            go.transform.SetParent(parent);
            go.transform.position = Wpos(w.x, w.y + w.z / 2f);
            var bc = go.AddComponent<BoxCollider2D>(); bc.isTrigger = true;
            bc.size = new Vector2(0.4f * cs.x, w.z * cs.y);
        }
    }

    // ===================== BOSS =====================
    static void BuildBoss()
    {
        var parent = Group("______Boss_____").transform;

        var boss = Inst("Assets/Prefab/Enemy/Boss_demon.prefab", parent, Wpos(198f + BO, 74.5f), "Boss_DemonSlime");
        var canvas = Inst("Assets/Prefab/Canvas-HealthBar-Boss.prefab", parent, Vector3.zero, "Canvas-HealthBar-Boss");
        var levelUp = Inst("Assets/Prefab/LevelUp.prefab", parent, Wpos(190f + BO, 76f), "LevelUp");
        if (levelUp != null) levelUp.SetActive(false);
        if (canvas != null) canvas.SetActive(false);

        var reward = new GameObject("F3_BossReward_WallJump");
        reward.transform.SetParent(parent);
        reward.transform.position = Wpos(189.5f + BO, 74.5f);
        var sr = reward.AddComponent<SpriteRenderer>();
        var crystal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Item/Crystal.prefab");
        var csr = crystal != null ? crystal.GetComponentInChildren<SpriteRenderer>() : null;
        if (csr != null) sr.sprite = csr.sprite;
        sr.color = new Color(0.5f, 0.9f, 1f); sr.sortingOrder = 5;
        var cc = reward.AddComponent<CircleCollider2D>(); cc.isTrigger = true; cc.radius = 0.6f;
        var ap = reward.AddComponent<AbilityPickup>();
        var apSo = new SerializedObject(ap);
        apSo.FindProperty("ability").intValue = (int)Ability.WallJump;
        apSo.ApplyModifiedPropertiesWithoutUndo();
        reward.SetActive(false);

        var trig = new GameObject("SpawnBoss_Trigger");
        trig.transform.SetParent(parent);
        trig.transform.position = Wpos(174.5f + BO, 76f);
        var tb = trig.AddComponent<BoxCollider2D>(); tb.isTrigger = true; tb.size = new Vector2(1f * cs.x, 6f * cs.y);
        var spawn = trig.AddComponent<SpawnBoss>();

        foreach (var bx in new[] { 170.5f + BO, 209.5f + BO })
        {
            var w = new GameObject("BossWall"); w.tag = "EnemyWall"; w.transform.SetParent(parent);
            w.transform.position = Wpos(bx, 76f);
            var b = w.AddComponent<BoxCollider2D>(); b.isTrigger = true; b.size = new Vector2(0.5f * cs.x, 6f * cs.y);
        }

        if (boss != null)
        {
            var bs = boss.GetComponent<Boss_Slime>();
            bs.nextMapPrefab = reward;
            bs.backMapPrefab = doorUp;
            bs.levelUp = levelUp;
            var hb = canvas != null ? canvas.GetComponent<HealthBar>() : null;
            var bso = new SerializedObject(bs);
            var hp = bso.FindProperty("healthBar");
            if (hp != null && hp.objectReferenceValue == null && hb != null) { hp.objectReferenceValue = hb; bso.ApplyModifiedPropertiesWithoutUndo(); }

            var flag = boss.GetComponent<DefeatFlag>(); if (flag == null) flag = boss.AddComponent<DefeatFlag>();
            var fso = new SerializedObject(flag);
            fso.FindProperty("flagId").stringValue = "F3_Boss_DemonSlime";
            fso.ApplyModifiedPropertiesWithoutUndo();

            spawn.bossSlimePrefab = bs;
            spawn.healthBoss = canvas;
            boss.SetActive(false);

            var stateGo = new GameObject("F3_BossRoomState");
            stateGo.transform.SetParent(parent);
            var st = stateGo.AddComponent<BossRoomState>();
            var sso = new SerializedObject(st);
            sso.FindProperty("boss").objectReferenceValue = flag;
            var dis = sso.FindProperty("disableIfDefeated");
            var objsOff = new GameObject[] { boss, trig, canvas };
            dis.arraySize = objsOff.Length;
            for (int k = 0; k < objsOff.Length; k++) dis.GetArrayElementAtIndex(k).objectReferenceValue = objsOff[k];
            var en = sso.FindProperty("enableIfDefeated");
            var objsOn = new GameObject[] { reward, doorUp };
            en.arraySize = objsOn.Length;
            for (int k = 0; k < objsOn.Length; k++) en.GetArrayElementAtIndex(k).objectReferenceValue = objsOn[k];
            sso.ApplyModifiedPropertiesWithoutUndo();
        }
        log.Add("Boss: Boss_demon (Boss_Slime) + thanh máu + phần thưởng Wall Jump + cửa F3_Up");
    }
}
