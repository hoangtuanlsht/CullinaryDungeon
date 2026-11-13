using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Cinemachine;

/// Greybox tầng 4 — Hồ Phát Sáng, đủ 30 phòng: A (hang dẫn), B (bờ hồ), C (mặt hồ), D (thành phố chìm), E (lòng cột trụ).
/// Theo tài liệu "Thiet-Ke-Tang-4-Ho-Phat-Sang.md" (mục 1b: 1 ô lưới = 18×10 tile, y lưới tăng xuống dưới).
/// - Nước hồ (R10, R12, R13) tạm là DeadZone. Nước thành phố chìm là lớp "F4_Water_Swim_TODO" chỉ để nhìn, KHÔNG gây chết:
///   chưa có cơ chế bơi nên tạm đi bộ trong đó để thử bố cục; sau này gắn WaterZone vào lớp này.
/// - Tường giả (vàng), song sắt (cam), cổng sau boss (đỏ), cửa dòng chảy (xanh) là khối chặn tạm, chưa có script phá/mở.
/// Chạy: Tools > Culinaria > Build Fourth Floor (F4). Chạy lại bao nhiêu lần cũng được (xóa phần đã dựng rồi dựng lại).
public static class FourthFloorBuilder
{
    const string SceneNameExpected = "FourthFloorScene";
    const int SW = 18, SH = 10;                       // 1 ô lưới = 1 khung camera
    const string TM = "Assets/TileMap/";
    static readonly Color GroundTint = new Color(0.92f, 1f, 1f);          // đá hang khu A–C: gần màu gốc, hơi xanh
    static readonly Color CityTint = new Color(0.62f, 0.98f, 0.92f);      // gạch tháp khu D–E: xanh ngọc
    static readonly Color CityBgTint = new Color(0.32f, 0.62f, 0.66f);    // gạch nền phía sau phòng khu D–E
    const string KD = "Assets/TileMap/KnightDungeon/";
    const string BrightMatPath = "Assets/BackGround/ThirdFloor/F3_KnightBright.mat";   // dùng lại material unlit của tầng 3
    const string CaveBgPath = "Assets/Resouces/Parallax_Backgrounds_Cave/0.png";
    const string F4BgFolder = "Assets/BackGround/FourthFloor/";                         // nếu bạn import bộ nền tầng 4 vào đây thì tự dùng
    static readonly Color WaterTint = new Color(0.18f, 0.90f, 0.79f, 0.75f);
    static readonly Color FakeWallTint = new Color(1f, 0.83f, 0.30f);
    static readonly Color GateTint = new Color(1f, 0.54f, 0.36f);
    static readonly Color GPTint = new Color(0.88f, 0.36f, 1f);
    static readonly Color BossGateTint = new Color(1f, 0.30f, 0.43f);
    static readonly Color CurrentTint = new Color(0.29f, 0.85f, 1f);
    static readonly Color SwimTint = new Color(0.35f, 0.95f, 1f, 0.10f);
    static readonly string[] GeneratedRoots = { "Grid_F4", "______Room_____", "______Door_____", "______Interact_____", "______Hidden_____", "______TODO_____", "______Background_____", "______Water_____",
                                                         "______RoomGate_____", "______HoleGlow_____", "______TunnelGlow_____", "______Waterfall_____" };   // 3 root cuối do F4RoomMerge/F4RoomFrames tạo

    class RoomDef
    {
        public string id; public int gx, gy, w, h; public RectInt r; public bool lake, underwater;
        public GameObject go; public Object vcam;
        public int BottomRow => gy + h - 1;                // hàng ô lưới thấp nhất của phòng
    }
    struct Door { public string a, b, side, kind; public int cx, cy, off; }

    static Dictionary<string, RoomDef> rooms;
    static readonly List<Door> doors = new List<Door>();
    const int minX = 0, minY = -160, W = 14 * SW, H = 160;   // vùng ô lưới x 0..13, y 0..15
    static bool[,] solid, plat, water, swim;
    static readonly List<string> log = new List<string>();
    static readonly List<(string name, RectInt area, string kind)> blockers = new List<(string, RectInt, string)>();

    static int CellBottom(int gy) => -(gy + 1) * SH;   // hàng tile thấp nhất của ô lưới
    static int CellLeft(int gx) => gx * SW;

    // ===================== DỮ LIỆU PHÒNG =====================
    static void R(string id, int gx, int gy, int w, int h, bool lake = false, bool underwater = false)
        => rooms[id] = new RoomDef { id = "F4_" + id, gx = gx, gy = gy, w = w, h = h, lake = lake, underwater = underwater,
               r = new RectInt(CellLeft(gx), CellBottom(gy + h - 1), w * SW, h * SH) };
    static void D(string a, string b, int cx, int cy, string side, int off = 8, string kind = "open")
        => doors.Add(new Door { a = a, b = b, cx = cx, cy = cy, side = side, off = off, kind = kind });

    static void DefineRooms()
    {
        rooms = new Dictionary<string, RoomDef>(); doors.Clear();
        // Khu A — hang dẫn
        R("R01", 1, 0, 1, 2); R("R02", 2, 1, 2, 1); R("R03", 4, 1, 1, 1); R("R04", 2, 2, 2, 1);
        // Khu B — bờ hồ & mỏm đá
        R("R05", 1, 3, 3, 1); R("R06", 4, 3, 1, 1); R("R07", 4, 4, 2, 2); R("R08", 6, 4, 1, 1);
        R("R09", 3, 4, 1, 2); R("R10", 2, 6, 2, 1); R("R11", 4, 6, 1, 1);
        // Khu C — mặt hồ
        R("R12", 5, 6, 3, 2, true); R("R13", 8, 7, 2, 1, true); R("R14", 10, 7, 1, 1); R("R15", 6, 8, 1, 2, false, true);
        // Khu D — thành phố chìm (R17, R23, R26 là buồng túi khí, khô)
        R("R16", 5, 10, 2, 1, false, true); R("R17", 7, 10, 1, 1); R("R18", 4, 11, 3, 1, false, true);
        R("R19", 3, 10, 1, 2, false, true); R("R20", 5, 12, 2, 2, false, true); R("R21", 4, 12, 1, 1, false, true);
        R("R22", 7, 11, 2, 1, false, true); R("R23", 7, 12, 1, 1); R("R24", 5, 14, 2, 1, false, true);
        R("R25", 7, 14, 1, 2, false, true); R("R26", 8, 15, 1, 1); R("R27", 9, 14, 3, 2, false, true);
        // Khu E — lòng cột trụ (khô)
        R("R28", 11, 13, 1, 1); R("R29", 10, 8, 1, 6); R("R30", 12, 13, 2, 1);

        D("R01", "R02", 1, 1, "right"); D("R02", "R03", 3, 1, "right"); D("R02", "R04", 3, 1, "bottom");
        D("R04", "R05", 3, 2, "bottom", 13);   // lệch phải: không rơi thẳng một mạch R02 → R04 → R05
        D("R05", "R06", 3, 3, "right"); D("R06", "R07", 4, 3, "bottom"); D("R07", "R08", 5, 4, "right");
        D("R07", "R09", 4, 5, "left"); D("R09", "R10", 3, 5, "bottom"); D("R10", "R11", 3, 6, "right");
        D("R07", "R11", 4, 5, "bottom", 3);    // lệch trái: không rơi thẳng từ lỗ R06 xuống R11
        D("R11", "R12", 4, 6, "right"); D("R12", "R13", 7, 7, "right"); D("R13", "R14", 9, 7, "right");
        D("R12", "R15", 6, 7, "bottom");
        D("R15", "R16", 6, 9, "bottom"); D("R16", "R17", 6, 10, "right");
        D("R16", "R18", 6, 10, "bottom", 3);   // lệch: không rơi thẳng R15 → R16 → R18 → R20
        D("R18", "R19", 4, 11, "left"); D("R18", "R22", 6, 11, "right"); D("R17", "R22", 7, 10, "bottom");
        D("R18", "R20", 6, 11, "bottom", 13); D("R20", "R21", 5, 12, "left"); D("R20", "R23", 6, 12, "right");
        D("R22", "R23", 7, 11, "bottom", 3);   // lệch: không rơi thẳng R17 → R22 → R23
        D("R20", "R24", 6, 13, "bottom"); D("R24", "R25", 6, 14, "right"); D("R25", "R26", 7, 15, "right");
        D("R26", "R27", 8, 15, "right");
        D("R27", "R28", 11, 14, "top", 8, "boss");          // mở sau khi hạ boss
        D("R28", "R29", 11, 13, "left"); D("R28", "R30", 11, 13, "right");
        D("R29", "R14", 10, 8, "top", 8, "inside");         // cổng cột trụ: mở từ trong R29
    }

    // ===================== MENU =====================
    [MenuItem("Tools/Culinaria/Build Fourth Floor (F4)")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneNameExpected)
        {
            EditorUtility.DisplayDialog("F4 Builder", "Hãy mở FourthFloorScene và đặt làm scene active trước.", "OK");
            return;
        }
        log.Clear(); blockers.Clear(); swimCells.Clear(); tunnelMid.Clear();
        // Sao lưu scene trước lần chia phòng kiểu Hollow Knight đầu tiên (giữ nguyên nếu đã có)
        const string backup = "Assets/Scenes/Backup/FourthFloorScene_2026-11-11_beforeMerge.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(backup) == null && !string.IsNullOrEmpty(scene.path))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Backup")) AssetDatabase.CreateFolder("Assets/Scenes", "Backup");
            AssetDatabase.CopyAsset(scene.path, backup);
            log.Add("Đã sao lưu scene: " + backup);
        }
        foreach (var root in scene.GetRootGameObjects())
            if (GeneratedRoots.Contains(root.name)) Object.DestroyImmediate(root);

        DefineRooms();
        BuildGeometry();
        WriteTilemaps();
        BuildWater();
        BuildUnderwaterDecor();
        BuildWaterfalls();
        BuildRooms();
        BuildDoorsAndInteract();
        BuildHiddenBlockers();
        BuildTodoMarkers();
        BuildBackgrounds();
        F4RoomMerge.ApplyAll(scene);   // gộp phòng + vệt sáng hầm + khung camera/cổng chặn quái kiểu Hollow Knight

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add($"Xong: {rooms.Count} phòng, {doors.Count} cửa, {blockers.Count} khối chặn (đường ẩn, cổng, dòng chảy).");
        foreach (var l in log) Debug.Log("[F4] " + l);
    }

    // ===================== HÌNH KHỐI =====================
    static bool In(int x, int y) => x >= minX && y >= minY && x < minX + W && y < minY + H;
    static void Set(bool[,] a, int x, int y, bool v) { if (In(x, y)) a[x - minX, y - minY] = v; }
    static bool Get(bool[,] a, int x, int y) => In(x, y) && a[x - minX, y - minY];
    static void Fill(int x, int y, int w, int h) { for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) Set(solid, x + i, y + j, true); }
    static void Carve(int x, int y, int w, int h)
    {
        for (int i = 0; i < w; i++) for (int j = 0; j < h; j++)
        { Set(solid, x + i, y + j, false); Set(plat, x + i, y + j, false); }
    }
    static void Plat(int x, int y, int w) { for (int i = 0; i < w; i++) if (!Get(solid, x + i, y)) Set(plat, x + i, y, true); }
    static void Water(int x, int y, int w, int h) { for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) if (!Get(solid, x + i, y + j)) Set(water, x + i, y + j, true); }

    static RoomDef RoomAtCell(int gx, int gy) =>
        rooms.Values.FirstOrDefault(r => gx >= r.gx && gx < r.gx + r.w && gy >= r.gy && gy < r.gy + r.h);

    /// Bậc một chiều rộng 3, so le, chia đều từ mặt sàn lên tới topSurface: mỗi bước ≤ 3 ô (tầm nhảy đơn).
    /// includeTop = true: đặt cả bậc ở topSurface (dưới lỗ trần, thẳng cột x).
    /// includeTop = false: topSurface là mặt một gờ đá có sẵn, chỉ xếp bậc bên dưới nó.
    static void ClimbStack(RoomDef room, int x, int topSurface, bool includeTop, int altDir = 0)
    {
        int ix0 = room.r.xMin + 1, ix1 = room.r.xMax - 2;
        int floorSurface = room.r.yMin + 1 + (room.lake ? 3 : 0);      // phòng hồ: tính từ mặt đá nổi
        int total = topSurface - floorSurface;
        if (total <= 3) return;
        int n = Mathf.CeilToInt(total / 3f);
        var ys = new List<int>();
        for (int i = 1; i <= n; i++)
        {
            if (i == n && !includeTop) break;
            ys.Add(floorSurface + Mathf.RoundToInt(total * i / (float)n) - 1);   // hàng tile = mặt bậc - 1
        }
        for (int i = 0; i < ys.Count; i++)
        {
            int px = x;
            int fromTop = ys.Count - 1 - i;                               // bậc trên cùng luôn ở cột x
            if (fromTop % 2 == 1)
                px = altDir != 0 ? x + 5 * altDir : ((x + 8 <= ix1) ? x + 5 : x - 5);
            px = Mathf.Clamp(px, ix0, ix1 - 2);
            Plat(px, ys[i], 3);
        }
    }

    static void BuildGeometry()
    {
        solid = new bool[W, H]; plat = new bool[W, H]; water = new bool[W, H]; swim = new bool[W, H];
        Fill(minX, minY, W, H);                                        // đá đặc toàn vùng
        foreach (var r in rooms.Values) Carve(r.r.xMin + 1, r.r.yMin + 1, r.r.width - 2, r.r.height - 2);

        // ---- Cửa giữa các phòng ----
        foreach (var d in doors)
        {
            RoomDef A = rooms[d.a], B = rooms[d.b];
            if (d.side == "right" || d.side == "left")
            {
                int bx = d.side == "right" ? (d.cx + 1) * SW : d.cx * SW;
                int yb = CellBottom(d.cy) + 1;
                bool lakeDoor = (A.lake || B.lake) && d.cy == A.BottomRow && d.cy == B.BottomRow;
                if (lakeDoor)
                {
                    yb += 2;                                                 // cửa cao hơn mặt nước
                    // bờ đá 5 ô trước cửa, ở cả hai phòng
                    Fill(d.side == "right" ? bx - 6 : bx + 1, yb - 2, 5, 2);
                    Fill(d.side == "right" ? bx + 1 : bx - 6, yb - 2, 5, 2);
                }
                Carve(bx - 1, yb, 2, 3);
                // Gờ đứng trước cửa nếu cửa nằm ở hàng trên của phòng cao
                foreach (var (room, cellY, inward) in new[] { (A, d.cy, d.side == "right" ? -1 : 1), (B, d.cy, d.side == "right" ? 1 : -1) })
                {
                    if (lakeDoor || cellY == room.BottomRow) continue;
                    int lx = inward < 0 ? bx - 6 : bx + 1;                   // gờ 5 ô sát tường
                    Fill(lx, yb - 1, 5, 1);
                    ClimbStack(room, inward < 0 ? lx - 4 : lx + 6, yb, false, inward < 0 ? -1 : 1);   // so le ra xa tường
                    log.Add($"Gờ + bậc leo trước cửa {d.a}–{d.b} trong {room.id}");
                }
            }
            else
            {
                int by = d.side == "bottom" ? CellBottom(d.cy) : CellBottom(d.cy) + SH;   // hàng tile thấp của biên
                int hx = CellLeft(d.cx) + d.off;
                Carve(hx, by - 1, 3, 2);
                RoomDef lower = d.side == "bottom" ? B : A;
                ClimbStack(lower, hx, lower.r.yMax - 3, true);   // chừa 2 ô dưới trần để bước ra được; lên lỗ cần Double Jump
                if (d.kind == "boss") blockers.Add(($"Gate_Boss_{A.id}_{B.id}", new RectInt(hx, by - 1, 3, 2), "boss"));
                if (d.kind == "inside") blockers.Add(($"Gate_{B.id}_{A.id} (mở từ trong {A.id})", new RectInt(hx, by - 1, 3, 2), "gate"));
            }
        }

        // ---- R01: gờ trên cùng cho cửa lên tầng 3 + bậc leo ----
        var r01 = rooms["R01"];
        Fill(r01.r.xMin + 1, r01.r.yMax - 9, 8, 1);                        // gờ rộng 8, mặt gờ cao hơn sàn 11 ô
        ClimbStack(r01, r01.r.xMin + 10, r01.r.yMax - 8, false);

        // ---- R09: khóa Wall Jump #1 — khe 3 ô sát tường trái, cột đá, gờ rương trên cao ----
        var r09 = rooms["R09"];
        int px9 = r09.r.xMin + 4;                                          // cột đá 2 ô
        Fill(px9, r09.r.yMin + 4, 2, 8);                                   // chân cột chừa 3 ô để chui vào khe từ sàn
        Fill(px9, r09.r.yMin + 11, 6, 1);                                  // gờ rương (mặt gờ cao hơn sàn 11 ô)
        ledgeChestY = r09.r.yMin + 12; ledgeChestX = px9 + 3.5f;

        // ---- R10: cầu gãy — hố 8 ô, đáy là nước tạm ----
        var r10 = rooms["R10"];
        int pit = r10.r.xMin + 8;
        Carve(pit, r10.r.yMin - 2, 8, 3);
        Water(pit, r10.r.yMin - 2, 8, 2);

        // ---- R12: giếng lặn xuống R15 — bậc đá 2 bên, nước giữa không gây chết ----
        var r12 = rooms["R12"];
        int dive = CellLeft(6) + 8;
        Fill(dive - 3, r12.r.yMin + 1, 3, 2); Fill(dive + 3, r12.r.yMin + 1, 3, 2);
        for (int x = dive; x < dive + 3; x++) for (int y = r12.r.yMin - 1; y <= r12.r.yMin + 2; y++) Set(swim, x, y, true);

        // ---- Hồ: R12, R13 — 2 hàng nước, đá nổi ----
        foreach (var lr in rooms.Values.Where(r => r.lake))
        {
            for (int x = lr.r.xMin + 1; x < lr.r.xMax - 1; x++)
                for (int y = lr.r.yMin + 1; y <= lr.r.yMin + 2; y++)
                    if (!Get(solid, x, y) && !Get(swim, x, y)) Set(water, x, y, true);
            // Rải đá nổi rộng 3 theo từng đoạn nước giữa hai bờ: mọi khe nước <= 3 ô (luật hố Module 5)
            for (int x = lr.r.xMin + 1; x <= lr.r.xMax - 2;)
            {
                if (!Get(water, x, lr.r.yMin + 1)) { x++; continue; }
                int a = x;
                while (x <= lr.r.xMax - 2 && Get(water, x, lr.r.yMin + 1)) x++;
                int L = x - a;
                int k = Mathf.Max(0, Mathf.CeilToInt((L - 3) / 6f));
                float gap = (L - 3f * k) / (k + 1);
                for (int i = 0; i < k; i++) Fill(a + Mathf.RoundToInt(gap * (i + 1) + 3 * i), lr.r.yMin + 3, 3, 1);
            }
        }

        // ---- R05: sàn nứt Ground Pound #1, bên dưới là hốc 3×3 ----
        var r05 = rooms["R05"];
        gp1 = new RectInt(r05.r.xMin + 23, r05.r.yMin, 3, 1);
        Carve(gp1.x, gp1.y - 3, 3, 4);

        // ---- R23: khóa Wall Jump #2 — cột đá kín từ sàn tới trần, khe leo đào sâu dưới sàn ----
        var r23 = rooms["R23"];
        int shaftX = r23.r.xMax - 4;                                         // khe 3 ô sát tường phải
        Fill(shaftX - 2, r23.r.yMin + 1, 2, r23.r.height - 2);              // cột 2 ô chặn từ sàn tới trần
        Carve(shaftX - 6, r23.r.yMin - 4, 9, 5);                            // hố sâu 5 ô dưới sàn, chui qua dưới cột
        Fill(shaftX - 2, r23.r.yMin - 1, 2, 2);                             // cột thò xuống hố 2 ô: chừa lối chui cao 3 ô, tường khe bắt đầu sát đáy
        Carve(shaftX, r23.r.yMin - 4, 3, r23.r.height - 1);                 // khe leo lên tới sát trần
        Carve(r23.r.xMax - 1, r23.r.yMin + 6, 6, 3);                        // hốc rương khoét vào tường phải
        chest3 = (r23.r.xMax + 2.5f, r23.r.yMin + 6);

        // ---- R24: Rương 4 trong hốc mép kênh (khoét xuống sàn, góc phải) ----
        var r24 = rooms["R24"];
        Carve(r24.r.xMax - 8, r24.r.yMin - 2, 6, 3);
        for (int x = r24.r.xMax - 8; x < r24.r.xMax - 2; x++) for (int y = r24.r.yMin - 2; y < r24.r.yMin; y++) swimCells.Add(new Vector2Int(x, y));
        chest4 = (r24.r.xMax - 5f, r24.r.yMin - 2);

        // ---- R29: khóa #3 — hành lang trong tường phải, khe Wall Jump 12 ô, hốc rương trên cao ----
        var r29 = rooms["R29"];
        int ny = CellBottom(10) + 1;                                        // sàn hành lang (hàng ô lưới 10)
        Fill(r29.r.xMax - 4, ny - 1, 3, 1);                                 // gờ đứng trước miệng hành lang
        Carve(r29.r.xMax - 1, ny, 9, 3);                                    // hành lang 9 ô vào tường phải
        Carve(r29.r.xMax + 5, ny, 3, 15);                                   // khe leo
        Carve(r29.r.xMax, ny + 12, 5, 3);                                   // hốc rương
        chest5 = (r29.r.xMax + 1.5f, ny + 12);

        // ---- Sàn nứt Ground Pound #2 (R22) và #3 (R30), bên dưới là hốc ----
        var r22 = rooms["R22"]; var r30 = rooms["R30"];
        gp2 = new RectInt(r22.r.xMin + 24, r22.r.yMin, 3, 1); Carve(gp2.x, gp2.y - 3, 3, 4);
        gp3 = new RectInt(r30.r.xMin + 24, r30.r.yMin, 3, 1); Carve(gp3.x, gp3.y - 3, 3, 4);

        // ---- Đường ẩn ----
        Tunnel("H1", new[] { (1, 3), (1, 2), (1, 1) }, "fake", "fake");
        Tunnel("H2", new[] { (6, 4), (6, 3), (5, 3), (5, 2), (4, 2), (3, 2) }, null, "gate");
        Tunnel("H3", new[] { (6, 6), (6, 5), (6, 4) }, null, "gate");
        Tunnel("H4", new[] { (6, 9), (7, 9), (7, 10) }, "fake", null, true);
        Tunnel("H5", new[] { (3, 11), (3, 12), (4, 12) }, null, "fake", true);
        Tunnel("H6", new[] { (10, 11), (9, 11), (8, 11) }, "gate", null);
        Tunnel("H7", new[] { (4, 12), (4, 13), (4, 14), (5, 14) }, null, "fake", true);
        Tunnel("CUR", new[] { (3, 10), (3, 9), (3, 8), (3, 7), (3, 6) }, null, "current", true);

        // ---- Tường dày kiểu Hollow Knight: ở 6 ranh giới ngang giữ lại, cửa thành hầm dài 8 ô (4 ô mỗi bên vách) ----
        // Đắp đá từ đỉnh cửa lên tới trần phòng (không lấn qua trần sang phòng trên). Phải khớp F4RoomMerge.Tunnels.
        int filled = 0;
        foreach (var d in doors)
        {
            if (!KeepTunnels.Contains((d.a, d.b)) || (d.side != "right" && d.side != "left")) continue;
            RoomDef A = rooms[d.a], B = rooms[d.b];
            int bx = d.side == "right" ? (d.cx + 1) * SW : d.cx * SW;
            int yb = CellBottom(d.cy) + 1;
            if ((A.lake || B.lake) && d.cy == A.BottomRow && d.cy == B.BottomRow) yb += 2;
            for (int x = bx - 4; x < bx + 4; x++)
            {
                var room = x >= A.r.xMin && x < A.r.xMax ? A : B;
                for (int y = yb + 3; y <= room.r.yMax - 2 && !Get(solid, x, y); y++) { Set(solid, x, y, true); Set(plat, x, y, false); filled++; }
            }
            log.Add($"Hầm {d.a}|{d.b}: x={bx} y={yb}");
        }
        log.Add($"Tường dày: đắp {filled} ô đá ở {KeepTunnels.Length} đường hầm");

        // ---- Nước thành phố chìm (chỉ để nhìn, chưa gây chết) ----
        foreach (var r in rooms.Values.Where(r => r.underwater))
            for (int x = r.r.xMin; x < r.r.xMax; x++)
                for (int y = r.r.yMin; y < r.r.yMax; y++)                 // cả viền phòng: lấp nước vào lỗ cửa nối hai phòng ngập (không tạo mặt nước giả)
                    if (!Get(solid, x, y)) Set(swim, x, y, true);
        foreach (var c in swimCells) if (!Get(solid, c.x, c.y)) Set(swim, c.x, c.y, true);
    }

    static readonly List<Vector2Int> swimCells = new List<Vector2Int>();
    static (float x, int y) chest3, chest4, chest5;
    static RectInt gp2, gp3;

    static float ledgeChestX; static int ledgeChestY;
    static RectInt gp1;

    static (int x, int y) Anchor(int gx, int gy) => (CellLeft(gx) + 8, CellBottom(gy) + 1);

    /// Hầm rộng/cao 3 ô đi qua tâm các ô đá trống; đầu/cuối là ô thuộc phòng.
    static void Tunnel(string id, (int gx, int gy)[] path, string startBlock, string endBlock, bool underwater = false)
    {
        for (int i = 0; i + 1 < path.Length; i++)
        {
            var a = Anchor(path[i].gx, path[i].gy); var b = Anchor(path[i + 1].gx, path[i + 1].gy);
            var rc = a.x == b.x ? new RectInt(a.x, Mathf.Min(a.y, b.y), 3, Mathf.Abs(a.y - b.y) + 3)
                                : new RectInt(Mathf.Min(a.x, b.x), a.y, Mathf.Abs(a.x - b.x) + 3, 3);
            Carve(rc.x, rc.y, rc.width, rc.height);
            if (underwater) foreach (var p in rc.allPositionsWithin) swimCells.Add(p);
        }
        AddBlocker(id, path[0], path[1], startBlock, "đầu");
        AddBlocker(id, path[path.Length - 1], path[path.Length - 2], endBlock, "cuối");
        // Bậc leo dưới lỗ trần trong phòng ở hai đầu (nếu lỗ nằm trên trần)
        foreach (var (roomCell, nb) in new[] { (path[0], path[1]), (path[path.Length - 1], path[path.Length - 2]) })
        {
            var room = RoomAtCell(roomCell.gx, roomCell.gy);
            if (room != null && nb.gx == roomCell.gx && nb.gy < roomCell.gy) ClimbStack(room, CellLeft(roomCell.gx) + 8, room.r.yMax - 3, true);
        }
        tunnelMid[id] = path.Skip(1).Take(path.Length - 2).Select(p => new Vector2Int(p.gx, p.gy)).ToList();   // ô lưới giữa = phòng hầm riêng
        log.Add($"Đường ẩn {id}: {path.Length - 2} ô đá");
    }

    static void AddBlocker(string id, (int gx, int gy) roomCell, (int gx, int gy) tunnelCell, string kind, string end)
    {
        if (kind == null) return;
        RectInt area;
        if (roomCell.gx == tunnelCell.gx)
        {
            int by = Mathf.Max(CellBottom(roomCell.gy), CellBottom(tunnelCell.gy));   // hàng đáy của ô phía trên
            area = new RectInt(CellLeft(roomCell.gx) + 8, by - 1, 3, 2);
        }
        else
        {
            int bx = Mathf.Max(roomCell.gx, tunnelCell.gx) * SW;
            area = new RectInt(bx - 1, CellBottom(roomCell.gy) + 1, 2, 3);
        }
        string room = RoomAtCell(roomCell.gx, roomCell.gy)?.id ?? "?";
        string prefix = kind == "fake" ? "FakeWall" : kind == "current" ? "Current" : "Gate";
        blockers.Add(($"{prefix}_{id}_{room}", area, kind));
    }

    // ===================== TILEMAP =====================
    static TileBase T(string path)
    {
        var t = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        if (t == null) log.Add("THIẾU tile: " + path);
        return t;
    }
    static Tilemap NewTilemap(Transform grid, string name, int order, Color tint, int layer = 0)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid, false);
        go.layer = layer;
        var tm = go.GetComponent<Tilemap>(); tm.color = tint;
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        return tm;
    }

    static Sprite blockSprite;

    // ===================== TILESET TẦNG 4 (16px) =====================
    const string TS = "Assets/BackGround/FourthFloor/F4_Tileset_Background/";
    const string CaveSheetPath = TS + "TrenMatNuoc/F4_CaveLake_16px.png";   // khu A–C: hồ hang
    const string CitySheetPath = TS + "DuoiNuoc/F4_Atlantis_16px.png";      // khu D–E: thành phố chìm
    const string TileOut = TS + "Tiles";                                      // Tile asset sinh ra từ 2 tileset
    static Dictionary<string, TileBase> tileCache;
    static Dictionary<Vector2Int, Sprite> caveSheet, citySheet;
    // vị trí ô trong bộ ghép tự động, theo mặt hở: index = trên<<3 | dưới<<2 | trái<<1 | phải
    static readonly Vector2Int[] AutoPos = {
        new Vector2Int(1,1), new Vector2Int(2,1), new Vector2Int(0,1), new Vector2Int(5,1),
        new Vector2Int(1,2), new Vector2Int(2,2), new Vector2Int(0,2), new Vector2Int(5,2),
        new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(0,0), new Vector2Int(5,0),
        new Vector2Int(1,3), new Vector2Int(2,3), new Vector2Int(0,3), new Vector2Int(3,2) };
    static readonly string[] BackWallRooms = { "R17", "R23", "R26", "R28", "R29", "R30" };   // buồng khô trong nhà / lòng cột trụ

    static void WriteTilemaps()
    {
        tileCache = new Dictionary<string, TileBase>();
        if (!AssetDatabase.IsValidFolder(TileOut)) AssetDatabase.CreateFolder(TS.TrimEnd('/'), "Tiles");
        caveSheet = LoadSheet(CaveSheetPath); citySheet = LoadSheet(CitySheetPath);
        var gridT = new GameObject("Grid_F4", typeof(Grid)).transform;
        int groundLayer = LayerMask.NameToLayer("Ground");
        blockSprite = (Cv(1, 1) as Tile)?.sprite;
        ComputeDist();
        deepCave = HasPixels(CaveSheetPath, 6, 4); deepCity = HasPixels(CitySheetPath, 0, 5);
        cityVar = HasPixels(CitySheetPath, 6, 7);   // tileset bản đá cổ có 3 biến thể cho mỗi ô mép trên/dưới/trái/phải
        if (!deepCave || !deepCity) log.Add("Tileset chưa có ô khối sâu (bản cũ) → toàn bộ đá đặc dùng texture viền. Ghi đè tileset mới để sửa.");

        // ---- Tường nền trong các buồng khô ----
        var wall = NewTilemap(gridT, "F4_City_BackWall", -12, Color.white);
        var wp = new List<Vector3Int>(); var wt = new List<TileBase>();
        foreach (var id in BackWallRooms)
        {
            var r = rooms[id].r;
            for (int x = r.xMin + 1; x < r.xMax - 1; x++) for (int y = r.yMin + 1; y < r.yMax - 1; y++)
            {
                if (Get(solid, x, y)) continue;
                int h = Hash(x, y) % 60;                               // ~7% ô có rêu/nứt, ~1.5% cửa sổ sáng
                wp.Add(new Vector3Int(x, y, 0)); wt.Add(Ct(4 + (h == 0 ? 2 : h < 3 ? 1 : h < 5 ? 3 : 0), 4));
            }
        }
        wall.SetTiles(wp.ToArray(), wt.ToArray());

        // ---- Đất: ghép tự động, khu A–C đá hang, khu D–E đá thành phố ----
        var ground = NewTilemap(gridT, "F4_Ground", 0, Color.white, groundLayer);
        var gp = new List<Vector3Int>(); var gt = new List<TileBase>();
        var pp = new List<Vector3Int>(); var pt = new List<TileBase>();
        int nCity = 0;
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
        {
            int x = i + minX, y = j + minY;
            if (solid[i, j])
            {
                bool city = CityStyle(x, y); if (city) nCity++;
                gp.Add(new Vector3Int(x, y, 0)); gt.Add(DepthTile(x, y, city));
            }
            else if (plat[i, j] && !water[i, j])
            {
                bool l = Get(plat, x - 1, y), r = Get(plat, x + 1, y);
                int part = !l && r ? 0 : l && !r ? 2 : 1;
                pp.Add(new Vector3Int(x, y, 0)); pt.Add(CityStyle(x, y) ? Ct(part, 6) : Cv(9 + part, 3));
            }
        }
        ground.SetTiles(gp.ToArray(), gt.ToArray());
        var tc = ground.gameObject.AddComponent<TilemapCollider2D>();
        var rb = ground.gameObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Static;
        ground.gameObject.AddComponent<CompositeCollider2D>();
#pragma warning disable 0618
        tc.usedByComposite = true;
#pragma warning restore 0618

        // ---- Bậc một chiều: bậc thang đá (hang) / bệ viền vàng (thành phố) ----
        var platTm = NewTilemap(gridT, "F4_Platform_OneWay", 0, Color.white, groundLayer);
        platTm.SetTiles(pp.ToArray(), pt.ToArray());
        var ptc = platTm.gameObject.AddComponent<TilemapCollider2D>();
        var prb = platTm.gameObject.AddComponent<Rigidbody2D>(); prb.bodyType = RigidbodyType2D.Static;
        var pcc = platTm.gameObject.AddComponent<CompositeCollider2D>();
#pragma warning disable 0618
        ptc.usedByComposite = true;
#pragma warning restore 0618
        pcc.usedByEffector = true;
        var eff = platTm.gameObject.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true; eff.surfaceArc = 160f; eff.useSideFriction = false;

        // ---- Trang trí (không va chạm) ----
        var decor = NewTilemap(gridT, "F4_Decor", 1, Color.white);
        var dp = new List<Vector3Int>(); var dt = new List<TileBase>();
        void Put(int x, int y, TileBase t) { if (t != null) { dp.Add(new Vector3Int(x, y, 0)); dt.Add(t); } }
        bool Free(int x, int y) => !Get(solid, x, y) && !Get(plat, x, y) && !Get(water, x, y);
        var used = new HashSet<Vector2Int>();
        foreach (var room in rooms.Values)
        {
            var r = room.r;
            for (int x = r.xMin + 2; x < r.xMax - 2; x++) for (int y = r.yMin + 1; y < r.yMax - 1; y++)
            {
                if (!Free(x, y) || used.Contains(new Vector2Int(x, y))) continue;
                bool city = CityStyle(x, y), floor = Get(solid, x, y - 1), ceil = Get(solid, x, y + 1);
                int h = Hash(x * 3 + 1, y * 7 + 2);
                if (floor && h % 9 == 0)
                {
                    int k = (h / 9) % 6;
                    if (!city) Put(x, y, k < 3 ? Cv(k, 4) : k < 5 ? Cv(3, 4) : Cv(11, 0));      // san hô 3 màu, nấm, măng đá
                    else if (k < 2 && Free(x, y + 1)) { Put(x, y, Ct(12 + k % 2, 5)); Put(x, y + 1, Ct(12 + k % 2, 4)); used.Add(new Vector2Int(x, y + 1)); }   // rong 2 ô
                    else Put(x, y, k < 4 ? Ct(14, 5) : Ct(15, 4));                                  // san hô, pha lê
                }
                else if (ceil && h % 8 == 1)
                {
                    int k = (h / 8) % 4;
                    if (!city)
                    {
                        if (k == 0 && Free(x, y - 1) && !Get(solid, x, y - 2)) { Put(x, y, Cv(8, 0)); Put(x, y - 1, Cv(8, 1)); used.Add(new Vector2Int(x, y - 1)); }   // thạch nhũ dài
                        else Put(x, y, k == 1 ? Cv(4, 4) : Cv(7, 0));                               // nấm treo / thạch nhũ
                    }
                    else if (k == 0) Put(x, y, Ct(14, 4));                                            // đèn treo
                }
            }
        }
        decor.SetTiles(dp.ToArray(), dt.ToArray());
        decorTm = decor; gridF4 = gridT;

        log.Add($"Tile: đất {gp.Count} (thành phố {nCity}), bậc một chiều {pp.Count}, tường nền {wp.Count}, trang trí {dp.Count}");
    }

    /// Đọc tileset 16px: chỉnh import (Sprite, Multiple, PPU 16, Point) và cắt lưới 16×16 nếu chưa cắt. Trả về (cột, hàng từ trên) → Sprite.
    static Dictionary<Vector2Int, Sprite> LoadSheet(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { log.Add("THIẾU tileset: " + path); return new Dictionary<Vector2Int, Sprite>(); }
        Dictionary<Vector2Int, Sprite> Map()
        {
            var d = new Dictionary<Vector2Int, Sprite>();
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
            {
                var rc = s.rect;
                if (Mathf.RoundToInt(rc.width) != 16 || Mathf.RoundToInt(rc.height) != 16) continue;
                d[new Vector2Int(Mathf.RoundToInt(rc.x) / 16, (s.texture.height - Mathf.RoundToInt(rc.yMax)) / 16)] = s;
            }
            return d;
        }
        var map = Map();
        bool ok = imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Multiple
                  && Mathf.Approximately(imp.spritePixelsPerUnit, 16f) && imp.filterMode == FilterMode.Point && map.Count >= 20;
        if (!ok)
        {
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = 16; imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false; imp.alphaIsTransparency = true;
            if (map.Count < 20)
            {
                imp.GetSourceTextureWidthAndHeight(out int tw, out int th);
                string bn = System.IO.Path.GetFileNameWithoutExtension(path);
                var metas = new List<SpriteMetaData>();
                for (int r = 0; r < th / 16; r++) for (int c = 0; c < tw / 16; c++)
                    metas.Add(new SpriteMetaData { name = $"{bn}_{c}_{r}", rect = new Rect(c * 16, th - (r + 1) * 16, 16, 16), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
#pragma warning disable 0618
                imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
            }
            imp.SaveAndReimport();
            map = Map();
            log.Add($"Đã chỉnh import {System.IO.Path.GetFileName(path)}: Sprite Multiple, PPU 16, Point, cắt 16×16 ({map.Count} ô)");
        }
        return map;
    }

    /// Tile asset cho ô (c, r) của tileset; lưu ở TileOut, chạy lại thì dùng lại.
    static TileBase Tl(Dictionary<Vector2Int, Sprite> sheet, string sheetName, int c, int r)
    {
        string key = $"F4{sheetName}_{c}_{r}";
        if (tileCache.TryGetValue(key, out var cached)) return cached;
        if (!sheet.TryGetValue(new Vector2Int(c, r), out var s)) { log.Add("THIẾU ô tileset " + key); tileCache[key] = null; return null; }
        string p = TileOut + "/" + key + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(p);
        if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, p); }
        tile.sprite = s; tile.colliderType = Tile.ColliderType.Grid;   // va chạm kín cả ô, không theo viền răng cưa của sprite
        EditorUtility.SetDirty(tile);
        tileCache[key] = tile; return tile;
    }
    static TileBase Cv(int c, int r) => Tl(caveSheet, "Cave", c, r);
    static TileBase Ct(int c, int r) => Tl(citySheet, "City", c, r);
    static bool SV(int x, int y) => !In(x, y) || Get(solid, x, y);

    static TileBase AutoTile(int x, int y, bool city)
    {
        int m = (SV(x, y + 1) ? 0 : 8) | (SV(x, y - 1) ? 0 : 4) | (SV(x - 1, y) ? 0 : 2) | (SV(x + 1, y) ? 0 : 1);
        if (m == 0)
        {
            if (!SV(x - 1, y + 1)) return city ? Ct(3, 0) : Cv(3, 0);   // góc trong
            if (!SV(x + 1, y + 1)) return city ? Ct(4, 0) : Cv(4, 0);
            if (!SV(x - 1, y - 1)) return city ? Ct(3, 1) : Cv(3, 1);
            if (!SV(x + 1, y - 1)) return city ? Ct(4, 1) : Cv(4, 1);
            int h = Hash(x, y) % 12;                                     // biến thể ô giữa
            if (h < 4) return city ? Ct(h, 4) : (h < 3 ? Cv(3 + h, 3) : Cv(1, 1));
        }
        var p = AutoPos[m];
        if (city && cityVar && (m == 8 || m == 4 || m == 2 || m == 1))
        {
            int h = Hash(x * 5 + 3, y * 11 + 1) % 4;                // 1/4 ô gốc, 3/4 biến thể đá cổ (nứt, rêu, tảo rủ)
            if (h > 0)
            {
                int i = h - 1;
                p = m == 8 ? new Vector2Int(6 + i, 7) : m == 4 ? new Vector2Int(9 + i, 7) : m == 2 ? new Vector2Int(12 + i, 7)
                  : (i == 0 ? new Vector2Int(15, 7) : new Vector2Int(5 + i, 0));
            }
        }
        return city ? Ct(p.x, p.y) : Cv(p.x, p.y);
    }

    static int[,] dist; static bool deepCave, deepCity, cityVar;

    /// Khoảng cách (8 hướng) từ mỗi ô đến ô trống gần nhất.
    static void ComputeDist()
    {
        dist = new int[W, H]; var q = new Queue<Vector2Int>();
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
            if (!solid[i, j]) { dist[i, j] = 0; q.Enqueue(new Vector2Int(i, j)); } else dist[i, j] = int.MaxValue;
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                int nx = p.x + dx, ny = p.y + dy;
                if (nx < 0 || ny < 0 || nx >= W || ny >= H || dist[nx, ny] <= dist[p.x, p.y] + 1) continue;
                dist[nx, ny] = dist[p.x, p.y] + 1; q.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    /// Đá đặc theo độ sâu: sát chỗ trống = viền có texture (ghép tự động), ô thứ 2 = chuyển tiếp tối, từ ô thứ 3 = khối tối gần trơn.
    static TileBase DepthTile(int x, int y, bool city)
    {
        int d = dist[x - minX, y - minY];
        if (d <= 1 || !(city ? deepCity : deepCave)) return AutoTile(x, y, city);
        int h = Hash(x, y) % 4;
        if (d == 2) return city ? Ct(4 + h, 5) : Cv(10 + h, 4);
        return city ? Ct(h, 5) : Cv(6 + h, 4);
    }

    /// Ô (c, r) của file PNG có nội dung không (đọc thẳng file, không cần bật Read/Write).
    static bool HasPixels(string path, int c, int r)
    {
        if (!System.IO.File.Exists(path)) return false;
        var tex = new Texture2D(2, 2);
        bool ok = tex.LoadImage(System.IO.File.ReadAllBytes(path)) && tex.GetPixel(c * 16 + 8, tex.height - (r * 16 + 8)).a > 0.5f;
        Object.DestroyImmediate(tex);
        return ok;
    }


    /// Ô thuộc khu thành phố chìm (hàng lưới ≥ 10, hoặc trong phòng R16–R30).
    static bool CityStyle(int x, int y)
    {
        int gx = Mathf.FloorToInt(x / (float)SW), gy = Mathf.FloorToInt((-y - 1) / (float)SH);
        if (gy >= 10) return true;
        var r = RoomAtCell(gx, gy);
        return r != null && IsCity(r);
    }


    /// Mỗi phòng dưới nước: một lớp màu nước trong mờ (sprite trắng 9-slice nhuộm màu).
    /// Chưa gắn script nên chưa có tác dụng gì; khi làm cơ chế bơi, gắn WaterZone vào các object này.
    /// NƯỚC TÁCH RIÊNG (root "______Water_____") để sau này viết code bơi:
    /// - F4_Water_Lake: nước hồ R10/R12/R13, tạm là DeadZone (trigger) như trước.
    /// - F4_Water_Swim: nước thành phố chìm + giếng lặn R12, chỉ để nhìn, KHÔNG collider.
    /// - WaterZones: mỗi phòng ngập một BoxCollider2D trigger đang TẮT, đúng vùng nước — bật lên khi có script bơi.
    static void BuildWater()
    {
        var root = new GameObject("______Water_____", typeof(Grid)).transform;
        var lake = NewTilemap(root, "F4_Water_Lake (DeadZone tạm)", 4, Color.white);
        lake.gameObject.tag = "DeadZone";
        var swimTm = NewTilemap(root, "F4_Water_Swim (chưa có script bơi)", 4, Color.white);
        var lp = new List<Vector3Int>(); var lt = new List<TileBase>();
        var sp = new List<Vector3Int>(); var st = new List<TileBase>();
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
        {
            int x = i + minX, y = j + minY;
            bool airAbove = !Get(solid, x, y + 1) && !Get(water, x, y + 1) && !Get(swim, x, y + 1);
            if (water[i, j]) { lp.Add(new Vector3Int(x, y, 0)); lt.Add(airAbove ? Cv(0, 5) : Cv(4, 5)); }
            else if (swim[i, j] && !solid[i, j])
            {
                bool city = CityStyle(x, y);
                sp.Add(new Vector3Int(x, y, 0));
                st.Add(city ? (airAbove ? Ct(0, 7) : Ct(4, 7)) : (airAbove ? Cv(0, 5) : Cv(4, 5)));
            }
        }
        lake.SetTiles(lp.ToArray(), lt.ToArray());
        lake.gameObject.AddComponent<TilemapCollider2D>().isTrigger = true;
        swimTm.SetTiles(sp.ToArray(), st.ToArray());
        // 3 khung sóng còn lại (wtop1–3) đã có sẵn Tile asset để sau làm AnimatedTile
        for (int f = 1; f < 4; f++) { Cv(f, 5); Ct(f, 7); }

        var zones = new GameObject("WaterZones (chưa có script bơi)").transform;
        zones.SetParent(root);
        int n = 0;
        foreach (var r in rooms.Values.Where(r => r.underwater).OrderBy(r => r.id))
        {
            var go = new GameObject("WaterZone_" + r.id);
            go.transform.SetParent(zones);
            go.transform.position = new Vector3(r.r.center.x, r.r.center.y - 0.5f, 0);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.isTrigger = true; bc.size = new Vector2(r.r.width - 2, r.r.height - 1);
            bc.enabled = false;   // tắt: chưa có cơ chế bơi; trigger lớn có thể làm raycast chạm đất nếu bật Queries Hit Triggers
            n++;
        }
        log.Add($"Nước (tách riêng): hồ DeadZone {lp.Count} ô, nước bơi {sp.Count} ô, {n} WaterZone (collider đang tắt)");
    }

    static Tilemap decorTm; static Transform gridF4;
    const string UwDecorPath = TS + "DuoiNuoc/F4_UnderwaterDecor_16px.png";
    // (cột, hàng trên cùng, rộng, cao) trong F4_UnderwaterDecor_16px.png
    static readonly (int c, int r, int w, int h)[] UwSmall = {
        (2,2,1,1), (3,2,1,1), (4,2,1,1), (5,2,1,1), (6,2,1,1), (7,2,1,1), (8,2,1,1), (9,2,1,1), (10,2,1,1), (11,2,1,1), (12,2,1,1), (8,3,1,1),
        (2,2,1,1), (3,2,1,1), (0,0,1,3), (1,0,1,3), (2,0,1,2) };          // cỏ biển lặp 2 lần cho hay gặp hơn
    static readonly (int c, int r, int w, int h)[] UwBig = { (5,0,2,2), (7,0,2,2), (9,0,2,2), (0,3,2,1), (2,3,2,1), (0,0,1,3) };
    static readonly (int c, int r, int w, int h)[] UwFloat = { (4,0,1,2), (4,3,2,1), (6,3,1,1), (7,3,1,1) };

    /// Đồ trang trí dưới nước (không va chạm) cho các phòng ngập: mọc đáy (rong, san hô, hải quỳ, sò, bình gốm, cột đổ, đầu tượng, mỏ neo),
    /// rủ từ trần (rong), trôi nổi (sứa, đàn cá, cột bong bóng). Tránh cửa, bậc, chỗ đã có trang trí.
    static void BuildUnderwaterDecor()
    {
        if (!System.IO.File.Exists(UwDecorPath)) { log.Add("Chưa có " + UwDecorPath + " → bỏ qua đồ trang trí dưới nước"); return; }
        var sheet = LoadSheet(UwDecorPath);
        var tm = NewTilemap(gridF4, "F4_Decor_Underwater", 2, Color.white);
        var used = new HashSet<Vector2Int>();
        var pos = new List<Vector3Int>(); var tiles = new List<TileBase>();
        bool Free(int x, int y) => In(x, y) && !Get(solid, x, y) && !Get(plat, x, y) && !Get(water, x, y) && !used.Contains(new Vector2Int(x, y))
                                   && (decorTm == null || !decorTm.HasTile(new Vector3Int(x, y, 0)));
        bool Fits(int x, int y, int w, int h, bool needFloor)
        {
            for (int i = 0; i < w; i++)
            {
                if (needFloor && !Get(solid, x + i, y - 1)) return false;
                for (int j = 0; j < h; j++) if (!Free(x + i, y + j)) return false;
            }
            return true;
        }
        void Place(int x, int y, (int c, int r, int w, int h) it, bool hangDown = false)
        {
            for (int i = 0; i < it.w; i++) for (int j = 0; j < it.h; j++)
            {
                int wy = hangDown ? y - j : y + (it.h - 1 - j);                  // hàng trên của sprite = ô cao nhất
                var t = Tl(sheet, "Uw", it.c + i, it.r + j);
                if (t == null) continue;
                pos.Add(new Vector3Int(x + i, wy, 0)); tiles.Add(t); used.Add(new Vector2Int(x + i, wy));
            }
        }
        int nFloor = 0, nCeil = 0, nFloat = 0;
        foreach (var room in rooms.Values.Where(r => r.underwater).OrderBy(r => r.id))
        {
            var r = room.r;
            for (int x = r.xMin + 3; x < r.xMax - 4; x++) for (int y = r.yMin + 1; y < r.yMax - 1; y++)
            {
                if (!Free(x, y)) continue;
                int h = Hash(x * 17 + 5, y * 31 + 7) % 1000;
                if (Get(solid, x, y - 1))                                            // đáy
                {
                    if (h < 110) { var it = UwBig[(h / 3) % UwBig.Length]; if (Fits(x, y, it.w, it.h, true)) { Place(x, y, it); nFloor++; continue; } }
                    if (h < 520) { var it = UwSmall[(h / 7) % UwSmall.Length]; if (Fits(x, y, it.w, it.h, true)) { Place(x, y, it); nFloor++; } }
                }
                else if (Get(solid, x, y + 1))                                       // trần
                {
                    if (h < 90 && Fits(x, y - 1, 1, 2, false)) { Place(x, y, (3, 0, 1, 2), true); nCeil++; }
                }
                else if (h < 14 && Fits(x - 1, y - 1, 4, 4, false))                    // trôi nổi giữa nước
                {
                    var it = UwFloat[(h + x) % UwFloat.Length]; Place(x, y, it); nFloat++;
                }
            }
        }
        tm.SetTiles(pos.ToArray(), tiles.ToArray());
        log.Add($"Đồ trang trí dưới nước: {nFloor} mọc đáy, {nCeil} rủ từ trần, {nFloat} trôi nổi ({pos.Count} ô)");
    }

    // ===================== THÁC NƯỚC (asset Resouces/WaterFall: 10 màu × 8 khung 48×48) =====================
    const string WaterfallDir = "Assets/Resouces/WaterFall/";
    /// (phòng, số thác tối đa, bộ màu dùng lần lượt): 1–4 xanh dương, 7 xanh ngọc
    static readonly (string room, int max, int[] sets)[] WaterfallRooms = { ("R12", 3, new[] { 3, 7, 1 }), ("R13", 1, new[] { 2 }), ("R10", 1, new[] { 7 }) };

    static Sprite[] WaterfallFrames(int set)
    {
        var list = new List<Sprite>();
        for (int f = 1; f <= 8; f++)
        {
            var s = BgSpritePath($"{WaterfallDir}{set}/Sprite-{f:0000}.png", true);   // Sprite Single, PPU 16, Point, Full Rect, Repeat
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }

    /// Thác đổ từ trần đá thẳng xuống mặt nước: chỉ đặt ở cột rộng 3 ô trống hoàn toàn (không bậc, không lỗ trần) từ trần tới nước.
    /// Chỉ là hình động (SpriteLoop), không va chạm, nằm sau nhân vật; chân thác chìm dưới lớp nước.
    static void BuildWaterfalls()
    {
        if (!AssetDatabase.IsValidFolder(WaterfallDir.TrimEnd('/'))) { log.Add("Chưa có " + WaterfallDir + " → bỏ qua thác nước"); return; }
        var root = new GameObject("______Waterfall_____").transform;
        bool Free3(int x, int y) => !Get(solid, x, y) && !Get(solid, x + 1, y) && !Get(solid, x + 2, y);
        bool Plat3(int x, int y) => Get(plat, x, y) || Get(plat, x + 1, y) || Get(plat, x + 2, y);
        int n = 0;
        foreach (var (rid, max, sets) in WaterfallRooms)
        {
            var r = rooms[rid].r;
            var cands = new List<(int x, int top, int surf)>();
            for (int x = r.xMin + 3; x <= r.xMax - 6; x++)
            {
                int surf = int.MinValue;
                for (int y = r.yMin - 3; y < r.yMax; y++)
                    if (Get(water, x, y) && Get(water, x + 1, y) && Get(water, x + 2, y) && !Get(water, x, y + 1)) { surf = y; break; }
                if (surf == int.MinValue) continue;
                int y2 = surf + 2; bool ok = !Plat3(x, surf + 1);   // bỏ qua hàng đá nổi ngay trên mặt hồ: thác vẽ sau tile nên đá che chân thác
                while (y2 < r.yMax && Free3(x, y2)) { if (Plat3(x, y2)) { ok = false; break; } y2++; }
                if (!ok || y2 - surf < 6) continue;
                if (Get(solid, x, y2) && Get(solid, x + 1, y2) && Get(solid, x + 2, y2)) cands.Add((x, y2, surf));   // trần đá liền
            }
            var chosen = new List<(int x, int top, int surf)>();
            foreach (var c in cands.OrderBy(c => Hash(c.x * 7 + 3, c.surf)))
            {
                if (chosen.Count >= max) break;
                if (chosen.Any(o => Mathf.Abs(o.x - c.x) < 12)) continue;
                chosen.Add(c);
            }
            for (int k = 0; k < chosen.Count; k++)
            {
                var (x, top, surf) = chosen[k];
                var frames = WaterfallFrames(sets[k % sets.Length]);
                if (frames.Length == 0) continue;
                var go = new GameObject($"Waterfall_{rid}_{k + 1}");
                go.transform.SetParent(root);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0]; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
                float bottom = surf + 0.3f, h = top - bottom;
                sr.size = new Vector2(3f, h); sr.sortingOrder = -2;
                go.transform.position = new Vector3(x + 1.5f, bottom + h / 2f, 0);
                var so = new SerializedObject(go.AddComponent<SpriteLoop>());
                var fp = so.FindProperty("frames"); fp.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                so.FindProperty("fps").floatValue = 10f;
                so.FindProperty("timeOffset").floatValue = 0.37f * (n + 1);
                so.ApplyModifiedPropertiesWithoutUndo();
                n++;
            }
            if (chosen.Count < max) log.Add($"Thác nước {rid}: chỉ tìm được {chosen.Count}/{max} cột trống từ trần tới nước");
        }
        log.Add($"Thác nước: {n} thác (R12, R13, R10)");
    }



    // ===================== ĐỘ SÁNG =====================
    const float CityBright = 2.2f;   // gạch tháp khu D–E
    const float CaveBright = 1.6f;   // đá hang + bậc đá khu A–C
    const float BgBright = 2.4f;     // gạch nền + ảnh nền
    static Material cityMat, caveMat, bgMat;

    /// Material Sprites/Default (unlit), màu nhân hệ số k (> 1 là sáng hơn ảnh gốc). Tạo mới nếu chưa có.
    static Material BrightMaterial(string path, float k)
    {
        if (!AssetDatabase.IsValidFolder(F4BgFolder.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/BackGround", "FourthFloor");
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
    static bool IsCity(RoomDef r) => int.Parse(r.id.Substring(4)) >= 16;              // R16–R30: thành phố chìm + cột trụ
    static bool CityCell(int x, int y) => rooms.Values.Any(r => IsCity(r) && r.r.Contains(new Vector2Int(x, y)));

    // ===================== ẢNH NỀN =====================
    static Sprite LoadBg(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(F4BgFolder + file);

    /// Phủ một sprite kín vùng phòng (kéo giãn cho vừa), đặt sau tilemap.
    /// Phủ một sprite kín vùng (kéo giãn cho vừa), đặt sau tilemap.
    static void CoverRoom(Transform parent, string name, Sprite s, RectInt area, int order, Color tint)
    {
        if (s == null) return;
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingOrder = order; sr.color = tint;
        Vector3 size = s.bounds.size;
        go.transform.localScale = new Vector3(area.width / size.x, area.height / size.y, 1);
        Vector3 c = Vector3.Scale(s.bounds.center, go.transform.localScale);
        go.transform.position = new Vector3(area.center.x, area.center.y, 0) - new Vector3(c.x, c.y, 0);
    }

    static readonly string[] CaveBgLayers = { "TrenMatNuoc/F4_BG_Cave_L0_Wall", "TrenMatNuoc/F4_BG_Cave_L1_Far", "TrenMatNuoc/F4_BG_Cave_L2_Ruins", "TrenMatNuoc/F4_BG_Cave_L3_Water", "TrenMatNuoc/F4_BG_Cave_L4_Front" };
    static readonly string[] SeaBgLayers = { "DuoiNuoc/F4_BG_L0_Sky", "DuoiNuoc/F4_BG_L1_FarCity", "DuoiNuoc/F4_BG_L2_Citadel", "DuoiNuoc/F4_BG_L3_MidCity", "DuoiNuoc/F4_BG_L35_FishParticles", "DuoiNuoc/F4_BG_L4_Seabed" };

    /// Ảnh nền nhiều lớp: R01–R14 dùng bộ hồ hang, R15–R30 dùng bộ thành phố chìm.
    /// Phòng quá dài/quá cao so với 16:9 thì chia thành nhiều mảnh để ảnh không bị kéo méo.
    /// Hiện là nền tĩnh, xếp L0 (xa) → L4 (gần); sau này có thể gắn script parallax vào từng lớp.
    /// Ảnh nền:
    /// - CẢNH ĐẶC TRƯNG chỉ dùng 1 lần: R07 (bờ hồ, tháp cầu thang xoắn) và R20 (giữa thành phố, thành kim tự tháp).
    ///   Cả hai phòng 2×2 ô = 36×20, gần đúng 16:9 nên ảnh không méo.
    /// - Phòng thường: nền chung lát lặp liền mạch (Generic/), đúng tỉ lệ pixel với tile:
    ///   hang = vách đá + dải thạch nhũ trên + dải cột mờ dưới; dưới nước = nước + tia sáng trên + thành phố mờ dưới.
    ///   Nước tối dần theo độ sâu.
    static void BuildBackgrounds()
    {
        var parent = Group("______Background_____").transform;
        var white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (white != null)   // nền tối phủ cả bản đồ: che các hốc ngoài khung phòng
        {
            var dark = new GameObject("BG_Dark_FullMap").AddComponent<SpriteRenderer>();
            dark.transform.SetParent(parent);
            dark.sprite = white; dark.drawMode = SpriteDrawMode.Sliced; dark.size = new Vector2(W, H);
            dark.color = new Color(0.04f, 0.07f, 0.16f); dark.sortingOrder = -40;
            dark.transform.position = new Vector3(minX + W / 2f, minY + H / 2f, 0);
        }
        var caveTile = BgSprite("Generic/F4_BG_Cave_Tile", true); var caveTop = BgSprite("Generic/F4_BG_Cave_Stalactites_Strip", true);
        var caveBot = BgSprite("Generic/F4_BG_Cave_Pillars_Strip", true);
        var seaTile = BgSprite("Generic/F4_BG_Sea_Tile", true); var seaTop = BgSprite("Generic/F4_BG_Sea_Rays_Strip", true);
        var seaBot = BgSprite("Generic/F4_BG_Sea_FarCity_Strip", true);

        var landmark = new Dictionary<string, string[]> { { "F4_R07", CaveBgLayers }, { "F4_R20", SeaBgLayers } };
        // Phòng boss R27 (54×20 = tỉ lệ 2.7): cắt cảnh 3 của bộ BackGroundUnderWater (xác thuyền + bóng cá mập), giữ đáy ảnh
        var bossBg = System.IO.File.Exists("Assets/Resouces/BackGroundUnderWater/background 3/orig.png")
            ? CropSprite("Assets/Resouces/BackGroundUnderWater/background 3/orig.png", new RectInt(0, 111, 576, 213)) : null;

        // ---- Bộ Ocean bạn tải về: phủ liền CẢ khu dưới nước (không lặp theo phòng) ----
        var ocean0 = BgSpritePath(F4BgFolder + "Midground/Ocean0.png", true);   // cột nước: mặt sáng → đáy tối
        var ocean1 = BgSpritePath(F4BgFolder + "Midground/Ocean1.png", false);  // xác tàu đắm (dùng 1 lần)
        var ocean2 = BgSpritePath(F4BgFolder + "Midground/Ocean2.png", true);   // đáy biển
        var seaRooms = rooms.Values.Where(r => int.Parse(r.id.Substring(4)) >= 15).ToList();
        int sx0 = seaRooms.Min(r => r.r.xMin), sx1 = seaRooms.Max(r => r.r.xMax), sy0 = seaRooms.Min(r => r.r.yMin), sy1 = seaRooms.Max(r => r.r.yMax);
        if (ocean0 != null)
        {
            var sr = new GameObject("Ocean_Water (Ocean0, cả khu dưới nước)").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent);
            sr.sprite = ocean0; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous; sr.sortingOrder = -34;
            float oh = ocean0.bounds.size.y;
            sr.size = new Vector2(sx1 - sx0, oh);
            float top = sy1 + 4;                                                   // đẩy dải mặt nước của ảnh lên trên, khuất sau khu hồ
            sr.transform.localScale = new Vector3(1, (top - sy0) / oh, 1);
            sr.transform.position = new Vector3((sx0 + sx1) / 2f, (sy0 + top) / 2f, 0);
        }
        if (ocean2 != null)
        {
            var sr = new GameObject("Ocean_Seabed (Ocean2, đáy bản đồ)").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent);
            sr.sprite = ocean2; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous; sr.sortingOrder = -33;
            float bh = ocean2.bounds.size.y;
            sr.size = new Vector2(sx1 - sx0, bh);
            sr.transform.position = new Vector3((sx0 + sx1) / 2f, sy0 + bh / 2f, 0);
        }
        if (ocean1 != null && bossBg == null)   // có nền xác thuyền riêng cho R27 thì không đặt tàu đắm Ocean1 nữa
        {
            var r27 = rooms["R27"].r;
            var sr = new GameObject("Ocean_Shipwreck (Ocean1, phòng boss R27)").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent);
            sr.sprite = ocean1; sr.sortingOrder = -32;
            sr.transform.position = new Vector3(r27.center.x + 6, r27.yMin + 3 + ocean1.bounds.size.y / 2f, 0) - new Vector3(ocean1.bounds.center.x, ocean1.bounds.center.y, 0);
        }
        int nGeneric = 0;
        foreach (var r in rooms.Values.OrderBy(r => r.id))
        {
            var rg = new GameObject("BG_" + r.id).transform; rg.SetParent(parent);
            if (r.id == "F4_R27" && bossBg != null)
            {
                CoverRoom(rg, "Landmark_BossShipwreck (BackGroundUnderWater 3)", bossBg, r.r, -31, new Color(0.72f, 0.95f, 1f));   // nhuộm sang xanh ngọc
                continue;
            }
            if (landmark.TryGetValue(r.id, out var layers))
            {
                // R07: cắt ảnh hồ hang để mặt hồ nằm ngay dưới sàn phòng, tháp ở giữa (ảnh gốc 640×360, mặt hồ ở hàng 230)
                RectInt? crop = r.id == "F4_R07" ? new RectInt(125, 0, 440, 244) : (RectInt?)null;
                for (int i = 0; i < layers.Length; i++)
                {
                    var s = crop.HasValue ? CropSprite(TS + layers[i] + ".png", crop.Value) : BgSprite(layers[i], false);
                    CoverRoom(rg, "Landmark_" + System.IO.Path.GetFileName(layers[i]), s, r.r, -30 + i, Color.white);
                }
                continue;
            }
            bool lakeSide = int.Parse(r.id.Substring(4)) <= 14;
            float depth = Mathf.Clamp01((r.BottomRow - 8) / 8f);                  // hàng lưới 8 → 16: sáng → tối
            Color seaTint = Color.Lerp(Color.white, new Color(0.62f, 0.70f, 0.78f), depth);
            if (lakeSide)
            {
                TiledBg(rg, "Cave_Wall", caveTile, r.r, -30, Color.white, 0);
                TiledBg(rg, "Cave_Pillars", caveBot, r.r, -29, Color.white, -1);
                TiledBg(rg, "Cave_Stalactites", caveTop, r.r, -28, Color.white, 1);
            }
            else
            {
                if (ocean0 == null) TiledBg(rg, "Sea_Water", seaTile, r.r, -30, seaTint, 0);
                if (ocean2 == null || r.BottomRow <= 13) TiledBg(rg, "Sea_FarCity", seaBot, r.r, -29, seaTint, -1);
                if (depth < 0.7f) TiledBg(rg, "Sea_Rays", seaTop, r.r, -28, new Color(1, 1, 1, 1 - depth), 1);
            }
            nGeneric++;
        }
        log.Add($"Nền: cảnh đặc trưng 1 lần ở R07 + R20, {nGeneric} phòng dùng nền chung lát lặp");
    }

    /// Lát lặp sprite (draw mode Tiled) đúng tỉ lệ PPU. anchor: 0 = phủ cả phòng, 1 = dải sát trần, -1 = dải sát đáy.
    static void TiledBg(Transform parent, string name, Sprite s, RectInt area, int order, Color tint, int anchor)
    {
        if (s == null) return;
        var go = new GameObject(name); go.transform.SetParent(parent);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
        sr.sortingOrder = order; sr.color = tint;
        float h = anchor == 0 ? area.height : Mathf.Min(s.bounds.size.y, area.height);
        sr.size = new Vector2(area.width, h);
        float cy = anchor == 0 ? area.center.y : anchor > 0 ? area.yMax - h / 2f : area.yMin + h / 2f;
        go.transform.position = new Vector3(area.center.x, cy, 0);
    }

    /// Cắt một vùng của ảnh nền (toạ độ pixel tính từ góc TRÊN-trái): để import Multiple với đúng 1 sprite là vùng cắt.
    /// File ảnh gốc không bị sửa, chỉ đổi cách import.
    static Sprite CropSprite(string path, RectInt crop)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { log.Add("THIẾU ảnh nền " + path); return null; }
        imp.GetSourceTextureWidthAndHeight(out int tw, out int th);
        string nm = System.IO.Path.GetFileNameWithoutExtension(path) + "_crop";
        var rect = new Rect(crop.x, th - crop.y - crop.height, crop.width, crop.height);
#pragma warning disable 0618
        var cur = imp.spritesheet;
        bool ok = imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Multiple && imp.filterMode == FilterMode.Point
                  && cur.Length == 1 && cur[0].name == nm && cur[0].rect == rect;
        if (!ok)
        {
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = 16; imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false;
            imp.spritesheet = new[] { new SpriteMetaData { name = nm, rect = rect, alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) } };
            imp.SaveAndReimport();
            log.Add($"Cắt nền {System.IO.Path.GetFileName(path)}: vùng {crop.width}×{crop.height} từ ({crop.x}, {crop.y})");
        }
#pragma warning restore 0618
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == nm);
    }


    static IEnumerable<RectInt> BgPieces(RectInt a)
    {
        float ratio = (a.width / (float)a.height) / (16f / 9f);
        int kx = ratio > 1.6f ? Mathf.RoundToInt(ratio) : 1, ky = ratio < 0.62f ? Mathf.RoundToInt(1f / ratio) : 1;
        int pw = a.width / kx, ph = a.height / ky;
        for (int i = 0; i < kx; i++) for (int j = 0; j < ky; j++)
            yield return new RectInt(a.xMin + i * pw, a.yMin + j * ph, i == kx - 1 ? a.width - i * pw : pw, j == ky - 1 ? a.height - j * ph : ph);
    }

    static Sprite BgSprite(string rel) => BgSprite(rel, false);
    /// tiled = true: chỉnh import cho lát lặp (Full Rect, Wrap Repeat).
    static Sprite BgSprite(string rel, bool tiled) => BgSpritePath(TS + rel + ".png", tiled);
    static Sprite BgSpritePath(string p, bool tiled)
    {
        var imp = AssetImporter.GetAtPath(p) as TextureImporter;
        if (imp == null) { log.Add("THIẾU ảnh nền " + p); return null; }
        var ts = new TextureImporterSettings(); imp.ReadTextureSettings(ts);
        bool ok = imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Single && imp.filterMode == FilterMode.Point
                  && Mathf.Approximately(imp.spritePixelsPerUnit, 16f)
                  && (!tiled || (ts.spriteMeshType == SpriteMeshType.FullRect && imp.wrapMode == TextureWrapMode.Repeat));
        if (!ok)
        {
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16; imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false;
            if (tiled)
            {
                imp.ReadTextureSettings(ts); ts.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(ts);
                imp.wrapMode = TextureWrapMode.Repeat;
            }
            imp.SaveAndReimport();
            log.Add("Đã chỉnh import ảnh nền " + System.IO.Path.GetFileName(p) + (tiled ? ": Sprite Single, Point, Full Rect, Repeat" : ": Sprite Single, Point"));
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    // ===================== ROOM (Module 5) =====================
    static GameObject Group(string name) => new GameObject(name);

    // ===================== KHUNG PHÒNG (chia lại như FirstFloorScene 1) =====================
    // - Mỗi phòng: collider gốc (nhận diện phòng) + con "CamBounds" (khung Confiner) + dải đệm "Buffer_*" 3 ô lấn qua cửa.
    // - Đường hầm ẩn (H1–H7, CUR) = phòng riêng phủ các ô lưới hầm đi qua → camera theo người chơi trong hầm.
    // - Hốc/ngách nằm ngoài khung phòng (hốc Ground Pound, hốc rương, ngách leo R29, hố R10/R23) = phòng nhỏ riêng,
    //   khung camera tối thiểu 1 màn hình 18×10 → vào hốc thì camera nhìn hốc, đi phía trên không lộ hốc.
    // - Camera: FramingTransposer damping 0.2, ScreenY 0.7 (giống tầng 1).
    static readonly Dictionary<string, List<Vector2Int>> tunnelMid = new Dictionary<string, List<Vector2Int>>();
    class Region { public string id; public HashSet<Vector2Int> detect = new HashSet<Vector2Int>(), cam = new HashSet<Vector2Int>(); public List<RectInt> cellRects = new List<RectInt>(); }
    /// 6 ranh giới ngang giữ lại (các cửa ngang khác được gộp phòng trong F4RoomMerge.Groups).
    static readonly (string, string)[] KeepTunnels = { ("R07", "R08"), ("R11", "R12"), ("R16", "R17"), ("R20", "R23"), ("R25", "R26"), ("R26", "R27") };

    static void BuildRooms()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Room.prefab");
        if (prefab == null) { log.Add("THIẾU prefab Assets/Prefab/Room.prefab"); return; }
        var parent = Group("______Room_____").transform;
        foreach (var r in rooms.Values.OrderBy(r => r.id))
        {
            var loop = new List<Vector2[]> { RectLoop(r.r) };
            r.go = MakeRoom(prefab, parent, r.id, r.r.min, loop, loop, out r.vcam);
        }
        // Dải lấn ở cửa do F4RoomFrames dựng (nới collider gốc), không cần Buffer_* nữa.
        var regions = ExtraRegions();
        foreach (var g in regions)
        {
            var min = new Vector2Int(g.detect.Min(c => c.x), g.detect.Min(c => c.y));
            var rgo = MakeRoom(prefab, parent, g.id, min, Trace(g.detect), Trace(g.cam), out _);
            for (int k = 0; k < g.cellRects.Count; k++)   // phòng hầm ẩn: mỗi ô lưới = 1 khung RoomRect để F4RoomFrames xử lý như phòng gộp
            {
                var c = new GameObject(k == 0 ? "RoomRect" : $"RoomRect_{g.id}_{k + 1}") { layer = rgo.layer };
                c.transform.SetParent(rgo.transform, false); c.transform.position = Vector3.zero;
                var bxc = c.AddComponent<BoxCollider2D>(); bxc.isTrigger = true; bxc.offset = g.cellRects[k].center; bxc.size = g.cellRects[k].size; bxc.enabled = false;
            }
        }
        log.Add($"Khung phòng: {rooms.Count} phòng + {regions.Count(g => !g.id.StartsWith("F4P_"))} hầm ẩn + {regions.Count(g => g.id.StartsWith("F4P_"))} hốc/ngách (trước khi gộp)");
    }

    static Vector2[] RectLoop(RectInt r) => new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };

    /// Tạo 1 phòng từ prefab (gỡ liên kết prefab như tầng 1 để đổi được thân camera). detect/cam: các vòng đa giác toạ độ thế giới.
    static GameObject MakeRoom(GameObject prefab, Transform parent, string id, Vector2Int origin, List<Vector2[]> detect, List<Vector2[]> cam, out Object vcamOut)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = id;
        int roomLayer = LayerMask.NameToLayer("Room");
        go.layer = roomLayer;
        go.transform.position = new Vector3(origin.x, origin.y, 0);
        Vector2 o = origin;
        var poly = go.GetComponent<PolygonCollider2D>();
        poly.offset = Vector2.zero; poly.pathCount = detect.Count;
        for (int i = 0; i < detect.Count; i++) poly.SetPath(i, detect[i].Select(p => p - o).ToArray());

        var cb = new GameObject("CamBounds"); cb.layer = roomLayer;
        cb.transform.SetParent(go.transform, false);
        var cpoly = cb.AddComponent<PolygonCollider2D>(); cpoly.isTrigger = true; cpoly.pathCount = cam.Count;
        for (int i = 0; i < cam.Count; i++) cpoly.SetPath(i, cam[i].Select(p => p - o).ToArray());

        var so = new SerializedObject(go.GetComponent<global::Room>());
        so.FindProperty("roomId").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        vcamOut = so.FindProperty("vcam").objectReferenceValue;
        if (vcamOut is CinemachineVirtualCamera vc)
        {
            var conf = vc.GetComponent<CinemachineConfiner2D>();
            if (conf != null) { conf.m_BoundingShape2D = cpoly; conf.m_Damping = 0; conf.InvalidateCache(); }
            if (vc.GetCinemachineComponent<CinemachineTransposer>() != null) vc.DestroyCinemachineComponent<CinemachineTransposer>();
            var ft = vc.GetCinemachineComponent<CinemachineFramingTransposer>() ?? vc.AddCinemachineComponent<CinemachineFramingTransposer>();
            ft.m_XDamping = 0.2f; ft.m_YDamping = 0.2f; ft.m_ZDamping = 1f;
            ft.m_ScreenX = 0.5f; ft.m_ScreenY = 0.7f; ft.m_DeadZoneWidth = 0; ft.m_DeadZoneHeight = 0;
            ft.m_SoftZoneWidth = 0.8f; ft.m_SoftZoneHeight = 0.8f; ft.m_LookaheadTime = 0; ft.m_CameraDistance = 10;
            vc.m_Lens.OrthographicSize = 5;
            var b = cpoly.bounds;
            vc.gameObject.name = "VCam_" + id;
            vc.transform.position = new Vector3(b.center.x, b.center.y, -10);
        }
        return go;
    }

    /// Dải đệm 3 ô lấn sang phòng bên kia ở mỗi cửa: còn đứng trong dải thì giữ phòng hiện tại (Room.Contains).
    static int DoorBuffers(Door d)
    {
        RoomDef A = rooms[d.a], B = rooms[d.b];
        if (A.go == null || B.go == null) return 0;
        if (d.side == "right" || d.side == "left")
        {
            int bx = d.side == "right" ? (d.cx + 1) * SW : d.cx * SW;
            int yb = CellBottom(d.cy) + 1;
            if ((A.lake || B.lake) && d.cy == A.BottomRow && d.cy == B.BottomRow) yb += 2;
            RoomDef left = d.side == "right" ? A : B, right = d.side == "right" ? B : A;
            AddBuffer(left, new RectInt(bx, yb - 1, 3, 5), right.id);
            AddBuffer(right, new RectInt(bx - 3, yb - 1, 3, 5), left.id);
        }
        else
        {
            int by = d.side == "bottom" ? CellBottom(d.cy) : CellBottom(d.cy) + SH;
            int hx = CellLeft(d.cx) + d.off;
            RoomDef upper = d.side == "bottom" ? A : B, lower = d.side == "bottom" ? B : A;
            AddBuffer(upper, new RectInt(hx - 1, by - 3, 5, 3), lower.id);
            AddBuffer(lower, new RectInt(hx - 1, by, 5, 3), upper.id);
        }
        return 2;
    }

    static void AddBuffer(RoomDef room, RectInt area, string other)
    {
        var go = new GameObject("Buffer_" + other); go.layer = LayerMask.NameToLayer("Room");
        go.transform.SetParent(room.go.transform, false);
        var bc = go.AddComponent<BoxCollider2D>(); bc.isTrigger = true;
        bc.size = area.size;
        bc.offset = area.center - (Vector2)room.go.transform.position;
    }

    /// Vùng trống nằm ngoài mọi khung phòng → phòng hầm ẩn / phòng hốc.
    static List<Region> ExtraRegions()
    {
        var inRoom = new bool[W, H];
        foreach (var r in rooms.Values)
            for (int x = r.r.xMin; x < r.r.xMax; x++) for (int y = r.r.yMin; y < r.r.yMax; y++) if (In(x, y)) inRoom[x - minX, y - minY] = true;
        bool Open(int x, int y) => In(x, y) && !solid[x - minX, y - minY] && !inRoom[x - minX, y - minY];
        var seen = new bool[W, H];
        var result = new Dictionary<string, Region>();
        var pocketCount = new Dictionary<string, int>();
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
        {
            int sx = i + minX, sy = j + minY;
            if (seen[i, j] || !Open(sx, sy)) continue;
            var comp = new List<Vector2Int>(); var q = new Queue<Vector2Int>();
            q.Enqueue(new Vector2Int(sx, sy)); seen[i, j] = true;
            while (q.Count > 0)
            {
                var p = q.Dequeue(); comp.Add(p);
                foreach (var dv in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var n = p + dv;
                    if (Open(n.x, n.y) && !seen[n.x - minX, n.y - minY]) { seen[n.x - minX, n.y - minY] = true; q.Enqueue(n); }
                }
            }
            if (comp.Count < 4) continue;
            // Hầm ẩn?
            string tid = null;
            foreach (var kv in tunnelMid)
                if (comp.Any(t => kv.Value.Contains(new Vector2Int(Mathf.FloorToInt(t.x / (float)SW), Mathf.FloorToInt((-t.y - 1) / (float)SH))))) { tid = kv.Key; break; }
            if (tid != null)
            {
                string id = "F4_" + tid;
                if (!result.TryGetValue(id, out var g)) result[id] = g = new Region { id = id };
                foreach (var c in tunnelMid[tid])
                {
                    int x0 = CellLeft(c.x), y0 = CellBottom(c.y);
                    g.cellRects.Add(new RectInt(x0, y0, SW, SH));
                    for (int x = x0; x < x0 + SW; x++) for (int y = y0; y < y0 + SH; y++) { g.detect.Add(new Vector2Int(x, y)); g.cam.Add(new Vector2Int(x, y)); }
                }
                continue;
            }
            // Hốc: thuộc phòng tiếp giáp nhiều nhất
            var owner = rooms.Values.OrderByDescending(r => comp.Count(t =>
                r.r.Contains(t + Vector2Int.right) || r.r.Contains(t + Vector2Int.left) || r.r.Contains(t + Vector2Int.up) || r.r.Contains(t + Vector2Int.down))).First();
            pocketCount.TryGetValue(owner.id, out int k); pocketCount[owner.id] = ++k;
            var pg = new Region { id = $"F4P_{owner.id.Substring(3)}_{k}" };   // F4P_* : F4RoomFrames bỏ qua (khung camera riêng ≥ 1 màn hình)
            int bx0 = comp.Min(t => t.x), bx1 = comp.Max(t => t.x) + 1, by0 = comp.Min(t => t.y), by1 = comp.Max(t => t.y) + 1;
            for (int x = bx0; x < bx1; x++) for (int y = by0; y < by1; y++) pg.detect.Add(new Vector2Int(x, y));
            int cw = Mathf.Max(SW, bx1 - bx0), ch = Mathf.Max(SH, by1 - by0);
            int cx0 = bx0 - (cw - (bx1 - bx0)) / 2, cy0 = by0 - (ch - (by1 - by0)) / 2;
            for (int x = cx0; x < cx0 + cw; x++) for (int y = cy0; y < cy0 + ch; y++) pg.cam.Add(new Vector2Int(x, y));
            result[pg.id] = pg;
        }
        return result.Values.OrderBy(g => g.id).ToList();
    }

    /// Viền của tập ô → các vòng đa giác (ngược chiều kim đồng hồ), bỏ điểm thẳng hàng.
    static List<Vector2[]> Trace(HashSet<Vector2Int> cells)
    {
        var next = new Dictionary<Vector2Int, List<Vector2Int>>();
        void Edge(Vector2Int a, Vector2Int b) { if (!next.TryGetValue(a, out var l)) next[a] = l = new List<Vector2Int>(); l.Add(b); }
        foreach (var c in cells)
        {
            int x = c.x, y = c.y;
            if (!cells.Contains(new Vector2Int(x, y - 1))) Edge(new Vector2Int(x, y), new Vector2Int(x + 1, y));
            if (!cells.Contains(new Vector2Int(x + 1, y))) Edge(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1));
            if (!cells.Contains(new Vector2Int(x, y + 1))) Edge(new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1));
            if (!cells.Contains(new Vector2Int(x - 1, y))) Edge(new Vector2Int(x, y + 1), new Vector2Int(x, y));
        }
        var loops = new List<Vector2[]>();
        while (next.Count > 0)
        {
            var start = next.Keys.First(); var cur = start; var pts = new List<Vector2Int>();
            Vector2Int dirIn = Vector2Int.zero;
            do
            {
                pts.Add(cur);
                var outs = next[cur];
                Vector2Int nxt = outs[0];
                if (outs.Count > 1 && dirIn != Vector2Int.zero)   // điểm kẹp: ưu tiên rẽ trái
                {
                    var left = new Vector2Int(-dirIn.y, dirIn.x);
                    foreach (var o in outs) if (o - cur == left) nxt = o;
                }
                outs.Remove(nxt); if (outs.Count == 0) next.Remove(cur);
                dirIn = nxt - cur; cur = nxt;
            } while (cur != start && next.ContainsKey(cur));
            var simp = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[(i - 1 + pts.Count) % pts.Count]; var b = pts[i]; var c = pts[(i + 1) % pts.Count];
                if ((b - a) != (c - b) * 1 && !((b.x - a.x) * (c.y - b.y) == (b.y - a.y) * (c.x - b.x))) simp.Add(b);
            }
            if (simp.Count >= 3) loops.Add(simp.ToArray());
        }
        return loops;
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

    static GameObject MakeDoor(Transform parent, string doorId, Vector3 pos, string targetScenePath, string targetDoorId, int enterDir)
    {
        var go = Inst("Assets/Prefab/SceneDoor.prefab", parent, pos, doorId);
        if (go == null) return null;
        go.transform.localScale = new Vector3(0.5f, 0.5f, 1);
        var so = new SerializedObject(go.GetComponent<SceneDoor>());
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
            spawn.position = pos + new Vector3(enterDir == 1 ? 2.5f : -2.5f, 0, 0);
            so.FindProperty("spawnPoint").objectReferenceValue = spawn;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    static void MakeChest(Transform parent, string id, float x, float yStand, int coins)
    {
        var go = Inst("Assets/Prefab/Chest.prefab", parent, new Vector3(x, yStand + 0.5f, 0), id);
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
        go.transform.position = new Vector3(x, yStand + 1.5f, 0);
        var bc = go.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = new Vector2(2, 3);
        var so = new SerializedObject(go.AddComponent<CheckPoint>());
        so.FindProperty("checkpointId").stringValue = id;
        var cam = so.FindProperty("areaCamera"); if (cam != null) cam.objectReferenceValue = room.vcam;
        so.ApplyModifiedPropertiesWithoutUndo();
        Inst("Assets/Prefab/CampFire.prefab", go.transform, new Vector3(x, yStand + 0.5f, 0), "CampFire");
    }

    static void BuildDoorsAndInteract()
    {
        var doorsT = Group("______Door_____").transform;
        var r01 = rooms["R01"].r;
        float ledgeStand = r01.yMax - 8;                                   // mặt gờ trên cùng R01
        // Giữ đúng tên cửa mà ThirdFloorBuilder đang trỏ tới: F3_Up → "F4_Down".
        MakeDoor(doorsT, "F4_Down", new Vector3(r01.xMin + 2.5f, ledgeStand + 1.5f, 0), "Assets/Scenes/ThirdFloorScene.unity", "F3_Up", 1);
        // Cửa ra tầng 5 (R30): đọc FifthFloorScene xem cửa nào đang trỏ về tầng 4 để đặt tên khớp
        var link = FindPartnerDoor("Assets/Scenes/FifthFloorScene.unity", SceneNameExpected);
        string myExit = link.myId ?? "F4_Exit", f5Door = link.theirId ?? "F5_Entry";
        if (link.theirId == null) log.Add("CHƯA KHỚP cửa tầng 5: FifthFloorScene chưa có SceneDoor trỏ về FourthFloorScene. Tạm đặt F4_Exit → F5_Entry, cần sửa khi dựng tầng 5.");
        else log.Add($"Cửa tầng 5: {myExit} → {f5Door} (đọc từ FifthFloorScene)");
        var r30 = rooms["R30"].r;
        MakeDoor(doorsT, myExit, new Vector3(r30.xMax - 3.5f, r30.yMin + 2.5f, 0), "Assets/Scenes/FifthFloorScene.unity", f5Door, 0);
        var sp = new GameObject("FourthFloorSpawnPoint"); sp.tag = "SpawnPoint";
        sp.transform.SetParent(doorsT); sp.transform.position = new Vector3(r01.xMin + 6.5f, ledgeStand + 1f, 0);

        var it = Group("______Interact_____").transform;
        var r04 = rooms["R04"]; var r08 = rooms["R08"];
        MakeCheckpoint(it, "F4_CP01", r04.r.xMin + 9.5f, r04.r.yMin + 1, r04);
        MakeCheckpoint(it, "F4_CP02", r08.r.xMin + 7.5f, r08.r.yMin + 1, r08);
        MakeChest(it, "F4_Chest01", ledgeChestX, ledgeChestY, 800);
        var r21 = rooms["R21"].r;
        MakeChest(it, "F4_Chest02", r21.xMin + 5.5f, r21.yMin + 1, 600);
        MakeChest(it, "F4_Chest03", chest3.x, chest3.y, 800);
        MakeChest(it, "F4_Chest04", chest4.x, chest4.y, 800);
        MakeChest(it, "F4_Chest05", chest5.x, chest5.y, 1200);
        var r17 = rooms["R17"]; var r26 = rooms["R26"];
        MakeCheckpoint(it, "F4_CP03", r17.r.xMin + 8.5f, r17.r.yMin + 1, r17);
        MakeCheckpoint(it, "F4_CP04", r26.r.xMin + 8.5f, r26.r.yMin + 1, r26);
        var boss = new GameObject("BossSpawn (boss tầng 4 - chưa thiết kế)");
        boss.transform.SetParent(it); boss.transform.position = new Vector3(rooms["R27"].r.center.x, rooms["R27"].r.center.y, 0);

        var safe = new GameObject("F4_SafeZone_TODO (Suối Mana R04)");
        safe.transform.SetParent(it);
        safe.transform.position = new Vector3(r04.r.center.x, r04.r.center.y, 0);
        var sb = safe.AddComponent<BoxCollider2D>(); sb.isTrigger = true;
        sb.size = new Vector2(r04.r.width - 2, r04.r.height - 2);
    }

    /// Mở scene (additive nếu chưa mở), tìm SceneDoor trỏ về mySceneName. Trả về (id cửa đó, id nó mong cửa của mình có).
    static (string theirId, string myId) FindPartnerDoor(string scenePath, string mySceneName)
    {
        if (!System.IO.File.Exists(scenePath)) return (null, null);
        Scene s = SceneManager.GetSceneByPath(scenePath);
        bool opened = false;
        if (!s.isLoaded) { s = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive); opened = true; }
        (string, string) result = (null, null);
        foreach (var root in s.GetRootGameObjects())
            foreach (var door in root.GetComponentsInChildren<SceneDoor>(true))
            {
                var so = new SerializedObject(door);
                var ts = so.FindProperty("targetScene");
                var pn = ts != null ? ts.FindPropertyRelative("sceneName") : null;
                if (pn != null && pn.stringValue == mySceneName)
                    result = (so.FindProperty("doorId").stringValue, so.FindProperty("targetDoorId").stringValue);
            }
        if (opened) EditorSceneManager.CloseScene(s, true);
        return result;
    }

    static GameObject Block(Transform parent, string name, RectInt a, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.layer = LayerMask.NameToLayer("Ground");
        go.transform.position = new Vector3(a.x + a.width / 2f, a.y + a.height / 2f, 0);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = blockSprite; sr.color = tint; sr.sortingOrder = 1;
        if (caveMat != null) sr.sharedMaterial = caveMat;
        Vector2 ss = blockSprite != null ? (Vector2)blockSprite.bounds.size : Vector2.one;   // kéo giãn 1 ô đá cho vừa khối
        go.transform.localScale = new Vector3(a.width / ss.x, a.height / ss.y, 1);
        go.AddComponent<BoxCollider2D>().size = ss;                                       // collider theo sprite, scale làm nó vừa khối
        return go;
    }

    static void BuildHiddenBlockers()
    {
        var hid = Group("______Hidden_____").transform;
        foreach (var b in blockers)
        {
            string note = b.kind == "fake" ? " (tường giả - chưa có script phá)"
                        : b.kind == "boss" ? " (mở sau khi hạ boss - chưa có script)"
                        : b.kind == "current" ? " (cửa dòng chảy - chặn tạm, chưa có bơi)"
                        : " (song sắt - chưa có script mở)";
            Color tint = b.kind == "fake" ? FakeWallTint : b.kind == "boss" ? BossGateTint : b.kind == "current" ? CurrentTint : GateTint;
            Block(hid, b.name + note, b.area, tint);
        }

        foreach (var (rect, id) in new[] { (gp1, "F4_R05_GP1"), (gp2, "F4_R22_GP2"), (gp3, "F4_R30_GP3") })
        {
            var gp = Block(hid, id + "_BreakableFloor", rect, GPTint);
            var so = new SerializedObject(gp.AddComponent<BreakableFloor>());
            so.FindProperty("floorId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Cần gạt (đánh dấu) cho song sắt: nằm ở phía mở được
        Marker(hid, "Lever_H2 (phía R08)", new Vector3(rooms["R04"].r.xMax + 3.5f, CellBottom(2) + 1.5f, 0));
        Marker(hid, "Lever_H3 (phía R12)", new Vector3(CellLeft(6) + 9.5f, CellBottom(5) + 7.5f, 0));
        Marker(hid, "Lever_H6 (phía R29)", new Vector3(rooms["R29"].r.xMin + 4.5f, CellBottom(11) + 1.5f, 0));
        Marker(hid, "Lever_CongCotTru (trong R29, đỉnh trục)", new Vector3(rooms["R29"].r.xMin + 4.5f, rooms["R29"].r.yMax - 4f, 0));
    }

    static void Marker(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = pos;
    }

    static void BuildTodoMarkers()
    {
        var todo = Group("______TODO_____").transform;
        var r12 = rooms["R12"].r;
        Marker(todo, "Khu D: thay F4_Water_Swim_TODO bằng WaterZone khi có cơ chế bơi", new Vector3(rooms["R20"].r.center.x, rooms["R20"].r.center.y, 0));
        Marker(todo, "R12: giếng lặn xuống R15 (nước không gây chết)", new Vector3(CellLeft(6) + 9.5f, r12.yMin + 1, 0));
        Marker(todo, "R19 → R10: dòng chảy một chiều (đang chặn ở sàn R10)", new Vector3(CellLeft(3) + 9.5f, CellBottom(8) + 5, 0));
        Marker(todo, "R27: phòng boss trống", new Vector3(rooms["R27"].r.center.x, rooms["R27"].r.yMin + 2, 0));
    }
}
