using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// [Tầng 5] Phần hình ảnh của FifthFloorBuilder: thay khối màu greybox bằng tile và sprite thật.
/// - Địa hình: tile của main_lev_buildB (castleB_*, cắt sẵn bởi F3ImportNewPacks; ở đây CHỈ ĐỌC).
/// - Vỏ nhà, mái, cửa, cửa sổ, đồ phố: BẢN SAO của MedievalVillageExterior và spr_Props trong
///   Assets/BackGround/FifthFloor/F5_Art (cắt riêng, để không đụng importer của tầng 3).
/// - Đồ trong nhà, đuốc, nến, tượng: tileset.png (g_*, cắt sẵn; chỉ đọc).
/// Mọi thứ nằm dưới "______Art_F5_____" và được dựng lại mỗi lần chạy builder.
public static partial class FifthFloorBuilder
{
    const string ArtDir = "Assets/BackGround/FifthFloor/F5_Art";
    const string SrcWalls = "Assets/Resouces/MedievalVillageExteriorv1.0/RawAssets/Walls.png";
    const string SrcRoofs = "Assets/Resouces/MedievalVillageExteriorv1.0/RawAssets/Roofs.png";
    const string SrcMProps = "Assets/Resouces/MedievalVillageExteriorv1.0/RawAssets/Props_Decor.png";
    const string SrcSProps = "Assets/Resouces/spr_Props.png";
    const string SrcCastleB = "Assets/Resouces/main_lev_buildB.png";
    const string SrcInterior = "Assets/Resouces/tileset.png";
    const string SrcNpc = "Assets/Resouces/Character/Sprite/idle-01.png";

    /// Nhà đã đặt bởi House(): phòng, góc dưới-trái (tile), rộng, số tầng, là lều orc hay không.
    static readonly List<(string room, int x0, int y0, int w, int floors, bool tent)> houses = new List<(string, int, int, int, int, bool)>();
    static Dictionary<string, Sprite> spr;
    static Dictionary<string, Tile> tiles;
    static Transform artRoot;

    // ---- Vùng cắt (đơn vị pixel, gốc dưới-trái như Unity) ----
    static readonly (string n, int x, int y, int w, int h)[] MPropRects =
    {
        ("f5_barrel", 4, 432, 44, 48), ("f5_crate", 48, 432, 48, 48), ("f5_bunting", 96, 445, 96, 24),
        ("f5_cart", 59, 384, 113, 48), ("f5_barrel2", 0, 347, 48, 47), ("f5_crateStack", 48, 288, 97, 85),
        ("f5_plank", 3, 289, 43, 25), ("f5_gable", 192, 230, 240, 224), ("f5_gableSlate", 192, 0, 240, 222),   // 240: cột 433+ là ống khói bên cạnh
        ("f5_chimney", 436, 301, 44, 76), ("f5_chimneySlate", 436, 110, 44, 81), ("f5_lamp", 16, 226, 18, 33),
        ("f5_fence", 48, 192, 48, 73), ("f5_planter", 96, 206, 96, 51), ("f5_winArch", 2, 123, 43, 61),
        ("f5_winSmall", 54, 127, 35, 44), ("f5_winMid", 96, 112, 48, 53), ("f5_winBig", 145, 117, 47, 62),
        ("f5_winFlower", 1, 14, 47, 75), ("f5_doorWood", 48, 0, 48, 92), ("f5_doorArch", 96, 0, 48, 85),
    };
    static readonly (string n, int x, int y, int w, int h)[] SPropRects =   // spr_Props 260×220, đã đổi sang gốc dưới-trái
    {
        ("s_bottle1", 29, 175, 11, 23), ("s_pot1", 53, 175, 16, 20), ("s_bottle2", 87, 175, 7, 18), ("s_bottle3", 111, 175, 6, 14),
        ("s_pot2", 132, 175, 10, 14), ("s_clothes", 153, 175, 49, 37), ("s_basket", 213, 175, 22, 7),
        ("s_wagon", 22, 123, 84, 29), ("s_cartSmall", 120, 123, 35, 18), ("s_bench", 185, 123, 44, 25),
        ("s_rubbleBig", 143, 77, 61, 25), ("s_rubble1", 20, 77, 16, 7), ("s_rubble2", 51, 77, 23, 12),
        ("s_rubble3", 92, 77, 36, 15), ("s_rubble4", 210, 77, 38, 16),
        ("s_hay", 18, 12, 62, 38), ("s_well", 93, 12, 44, 48), ("s_sign", 148, 12, 22, 50), ("s_barrel", 183, 12, 20, 23), ("s_stump", 214, 12, 24, 25),
    };

    // ===================== CHUẨN BỊ TÀI NGUYÊN =====================
    static bool PrepareArt()
    {
        foreach (var p in new[] { SrcWalls, SrcRoofs, SrcMProps, SrcSProps, SrcCastleB, SrcInterior })
            if (!System.IO.File.Exists(p)) { log.Add("THIẾU tài nguyên: " + p + " → bỏ qua phần hình ảnh."); return false; }
        if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/BackGround/FifthFloor", "F5_Art");
        if (!AssetDatabase.IsValidFolder(ArtDir + "/Tiles")) AssetDatabase.CreateFolder(ArtDir, "Tiles");

        string walls = CopyOnce(SrcWalls, "F5_Walls.png"), roofs = CopyOnce(SrcRoofs, "F5_Roofs.png");
        string mprops = CopyOnce(SrcMProps, "F5_MedievalProps.png"), sprops = CopyOnce(SrcSProps, "F5_StreetProps.png");
        Slice(walls, 24, SpriteAlignment.BottomLeft, Enumerable.Range(0, 4).Select(i => ("f5_wall" + i, i * 96, 0, 96, 96)).ToArray());
        Slice(roofs, 24, SpriteAlignment.BottomLeft, Enumerable.Range(0, 2).Select(i => ("f5_roof" + i, i * 96, 0, 96, 96)).ToArray());
        Slice(mprops, 24, SpriteAlignment.BottomCenter, MPropRects);
        Slice(sprops, 16, SpriteAlignment.BottomCenter, SPropRects);

        spr = new Dictionary<string, Sprite>();
        foreach (var path in new[] { walls, roofs, mprops, sprops, SrcCastleB, SrcInterior, SrcNpc })
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()) spr[s.name] = s;

        tiles = new Dictionary<string, Tile>();
        foreach (var n in GroundTopS.Concat(GroundFillS).Concat(GroundTopA).Concat(GroundFillA).Concat(new[] { BeamTile }))
        {
            if (!spr.TryGetValue(n, out var s)) { log.Add("THIẾU sprite " + n + " trong main_lev_buildB (chạy Tools/Culinaria/F3 Import New Packs?)"); return false; }
            string tp = ArtDir + "/Tiles/T_" + n + ".asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(tp);
            if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, tp); }
            t.sprite = s; t.colliderType = Tile.ColliderType.Grid; t.flags = TileFlags.None;
            EditorUtility.SetDirty(t);
            tiles[n] = t;
        }
        return true;
    }

    /// Chép file ảnh (KHÔNG chép .meta) để bản sao có ID sprite mới, không mang theo cách cắt của tầng 3.
    static string CopyOnce(string src, string fileName)
    {
        string dst = ArtDir + "/" + fileName;
        string meta = dst + ".meta";
        bool stale = System.IO.File.Exists(meta) && System.IO.File.ReadAllText(meta).Contains("m_barrel");   // bản chép cũ còn sprite của tầng 3
        if (stale) { AssetDatabase.DeleteAsset(dst); log.Add("Chép lại (bỏ meta cũ): " + dst); }
        if (!System.IO.File.Exists(dst))
        {
            System.IO.File.Copy(src, dst);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            log.Add("Chép " + src + " → " + dst);
        }
        return dst;
    }

    static void Slice(string path, float ppu, SpriteAlignment align, (string n, int x, int y, int w, int h)[] rects)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        var set = new TextureImporterSettings(); ti.ReadTextureSettings(set);
        set.spriteMeshType = SpriteMeshType.FullRect;      // cần cho SpriteRenderer Draw Mode = Tiled
        ti.SetTextureSettings(set);
#pragma warning disable 0618
        ti.spritesheet = rects.Select(r => new SpriteMetaData
        {
            name = r.n, rect = new Rect(r.x, r.y, r.w, r.h), alignment = (int)align, pivot = Vector2.zero
        }).ToArray();
#pragma warning restore 0618
        ti.SaveAndReimport();
    }

    // ===================== TILE ĐỊA HÌNH =====================
    static readonly string[] GroundTopS = { "castleB_23_1", "castleB_24_1", "castleB_25_1" };   // gạch đá xanh, mép sáng (thành phố, mê cung)
    static readonly string[] GroundFillS = { "castleB_24_2", "castleB_24_2", "castleB_23_2", "castleB_25_2", "castleB_18_3" };
    static readonly string[] GroundTopA = { "castleB_7_1", "castleB_8_1", "castleB_9_1" };      // gạch nâu (trại orc, hầm lương)
    static readonly string[] GroundFillA = { "castleB_1_3", "castleB_2_3", "castleB_3_3", "castleB_4_3", "castleB_5_3" };
    const string BeamTile = "castleB_2_6";                                                       // dầm gỗ: sàn tầng nhà, bậc

    static int Hash(int x, int y) { unchecked { int h = x * 73856093 ^ y * 19349663; return h < 0 ? -h : h; } }

    static (TileBase tile, Color tint) GroundTileAt(int x, int y)
    {
        bool airAbove = In(x, y + 1) && !Get(solid, x, y + 1);
        bool camp = y >= -30;
        int ix = ((x % 3) + 3) % 3;
        string n = camp ? (airAbove ? GroundTopA[ix] : GroundFillA[Hash(x, y) % GroundFillA.Length])
                        : (airAbove ? GroundTopS[ix] : GroundFillS[Hash(x, y) % GroundFillS.Length]);
        Color c = camp ? Color.white : y >= -60 ? new Color(0.95f, 0.97f, 1f) : y >= -80 ? new Color(0.85f, 1f, 0.97f)
                : IsBossArea(x, y) || y < -140 ? new Color(0.9f, 0.62f, 0.62f) : new Color(0.62f, 0.62f, 0.78f);
        return (tiles[n], c);
    }

    static void PaintGroundArt(Tilemap tm)
    {
        var ps = new List<Vector3Int>(); var ts = new List<TileBase>(); var cs = new List<Color>();
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
        {
            if (!solid[i, j]) continue;
            int x = i + minX, y = j + minY;
            var (t, c) = GroundTileAt(x, y);
            ps.Add(new Vector3Int(x, y, 0)); ts.Add(t); cs.Add(c);
        }
        tm.SetTiles(ps.ToArray(), ts.ToArray());
        for (int k = 0; k < ps.Count; k++) tm.SetColor(ps[k], cs[k]);
    }

    static void PaintPlatformArt(Tilemap tm)
    {
        var ps = new List<Vector3Int>();
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
            if (plat[i, j] && !solid[i, j]) ps.Add(new Vector3Int(i + minX, j + minY, 0));
        tm.SetTiles(ps.ToArray(), Enumerable.Repeat<TileBase>(tiles[BeamTile], ps.Count).ToArray());
        foreach (var p in ps) tm.SetColor(p, p.y >= -30 ? new Color(1f, 0.9f, 0.8f) : new Color(0.85f, 0.85f, 0.9f));
    }

    // ===================== ĐẶT SPRITE =====================
    /// Đặt sprite sao cho đáy-giữa của nó nằm ở (x, y), bất kể pivot của sprite.
    static SpriteRenderer Put(Transform parent, string name, float x, float y, int order, Color tint, float sx = 1, float sy = 1, bool flip = false)
    {
        if (!spr.TryGetValue(name, out var s)) { log.Add("Thiếu sprite " + name); return null; }
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localScale = new Vector3(flip ? -sx : sx, sy, 1);
        var b = s.bounds;
        go.transform.position = new Vector3(x - b.center.x * (flip ? -sx : sx), y - b.min.y * sy, 0);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingOrder = order; sr.color = tint;
        return sr;
    }

    /// Đặt sprite và kéo giãn cho vừa khung rộng w, cao h (đáy-giữa ở x, y).
    static SpriteRenderer PutFit(Transform parent, string name, float x, float y, float w, float h, int order, Color tint)
    {
        if (!spr.TryGetValue(name, out var s)) { log.Add("Thiếu sprite " + name); return null; }
        return Put(parent, name, x, y, order, tint, w / s.bounds.size.x, h / s.bounds.size.y);
    }

    static SpriteRenderer Tiled(Transform parent, string name, float x0, float y0, float w, float h, int order, Color tint)
    {
        if (!spr.TryGetValue(name, out var s)) { log.Add("Thiếu sprite " + name); return null; }
        var go = new GameObject(name + "_Tiled");
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(x0, y0, 0);                      // sprite tường có pivot dưới-trái
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(w, h); sr.sortingOrder = order; sr.color = tint;
        return sr;
    }

    static GameObject Torch(Transform parent, float x, float y, float phase)
    {
        var sr = Put(parent, "g_torch0", x, y, -4, Color.white);
        if (sr == null) return null;
        var frames = new[] { "g_torch0", "g_torch1", "g_torch2", "g_torch3" }.Where(spr.ContainsKey).Select(k => spr[k]).ToArray();
        var loop = sr.gameObject.AddComponent<F5SpriteLoop>();
        var so = new SerializedObject(loop);
        var fp = so.FindProperty("frames"); fp.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        so.FindProperty("phase").floatValue = phase;
        so.ApplyModifiedPropertiesWithoutUndo();
        sr.gameObject.name = "Torch (animation)";
        return sr.gameObject;
    }

    // ===================== TRANG TRÍ =====================
    static readonly Color UpperTint = new Color(0.80f, 0.82f, 0.92f);   // phố trên: đá lạnh, còn khá sạch
    static readonly Color LowerTint = new Color(0.70f, 0.76f, 0.80f);   // phố dưới: tối và cũ hơn
    static readonly Color TentTint = new Color(0.86f, 0.52f, 0.40f);    // lều da thuộc của orc
    static readonly Color Dim = new Color(0.72f, 0.72f, 0.80f);

    static void DecorateF5(UnityEngine.SceneManagement.Scene scene)
    {
        artRoot = new GameObject("______Art_F5_____").transform;
        BuildBackdrop(scene);
        BuildFacades();
        BuildStreetProps();
        BuildNpcPlaceholders();
        BuildFogRig(scene);
        log.Add($"Hình ảnh: {houses.Count(h => !h.tent)} vỏ nhà, {houses.Count(h => h.tent)} lều, {artRoot.GetComponentsInChildren<SpriteRenderer>().Length} sprite.");
    }

    static void BuildBackdrop(UnityEngine.SceneManagement.Scene scene)
    {
        var bd = new GameObject("Backdrop").transform; bd.SetParent(artRoot);
        void Band(string n, float yTop, float yBot, Color c, float xL = minX, float xR = minX + W)
        {
            var go = new GameObject(n); go.transform.SetParent(bd);
            go.transform.position = new Vector3((xL + xR) / 2f, (yTop + yBot) / 2f, 0);
            Vector2 ss = blockSprite.bounds.size;
            go.transform.localScale = new Vector3((xR - xL) / ss.x, (yTop - yBot) / ss.y, 1);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = blockSprite; sr.color = c; sr.sortingOrder = -100;
        }
        Band("Band_A_TraiOrc", minY + H, -30, new Color(0.10f, 0.07f, 0.06f));
        Band("Band_B_PhoTren", -30, -60, new Color(0.06f, 0.08f, 0.12f));
        Band("Band_C_PhoDuoi", -60, -80, new Color(0.05f, 0.09f, 0.10f));
        Band("Band_D_MeCung", -80, minY, new Color(0.03f, 0.03f, 0.05f));
        Band("Band_E_Boss", -130, minY, new Color(0.08f, 0.03f, 0.03f), minX, 90);   // chỉ dưới R42, R43; R38, R40 vẫn là mê cung

        // Ảnh nền phố có sẵn của scene cũ (đang tắt) dùng làm nền xa cho phòng mốc
        Sprite FindOld(string goName)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == goName && t.GetComponent<SpriteRenderer>() != null) return t.GetComponent<SpriteRenderer>().sprite;
            return null;
        }
        void Landmark(string goName, string roomId)
        {
            var s = FindOld(goName); if (s == null) { log.Add("Không thấy ảnh nền cũ " + goName); return; }
            var r = rooms[roomId].r;
            var go = new GameObject("Landmark_" + goName + "_" + roomId); go.transform.SetParent(bd);
            float h = r.height - 2, k = h / s.bounds.size.y;
            go.transform.localScale = new Vector3(k, k, 1);
            go.transform.position = new Vector3(r.center.x - s.bounds.center.x * k, r.yMin + 1 - s.bounds.min.y * k, 0);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = -90; sr.color = new Color(0.42f, 0.42f, 0.52f);
        }
        Landmark("Map3", "R10");     // thị trấn đèn ấm, tháp tượng rồng → cổng thành
        Landmark("Map4", "R20");     // thánh đường gô-tích, pha lê → thánh đường đổ
    }

    static void BuildFacades()
    {
        var root = new GameObject("Houses").transform; root.SetParent(artRoot);
        int idx = 0;
        foreach (var hs in houses)
        {
            var room = rooms[hs.room];
            var hT = new GameObject($"{(hs.tent ? "Tent" : "House")}_{hs.room}_{hs.x0 - room.r.xMin}").transform; hT.SetParent(root);
            float cx = hs.x0 + hs.w / 2f;
            if (hs.tent)
            {
                PutFit(hT, "f5_gable", cx, hs.y0, hs.w + 1, 5.2f, -29, TentTint);          // lều chữ A: mái hồi làm vải lều
                Put(hT, "f5_fence", hs.x0 - 0.6f, hs.y0, -28, Color.white, 0.8f, 0.8f);
                idx++; continue;
            }
            bool upper = hs.y0 > -60;
            Color tint = upper ? UpperTint : LowerTint;
            int wall = upper ? (idx % 2 == 0 ? 2 : 3) : (idx % 2 == 0 ? 0 : 1);   // phố trên: đá / nửa gỗ; phố dưới: gạch khung gỗ / vữa
            if (hs.room == "R13" && hs.w > 20) wall = 0;                            // quán rượu: gạch khung gỗ
            int top = hs.y0 + 5 * hs.floors;                                         // mặt mái (nơi đứng được)
            Tiled(hT, "f5_wall" + wall, hs.x0, hs.y0, hs.w, 5 * hs.floors, -31, tint);           // cao tới mặt mái, che khe dưới dầm

            // mái hồi + ống khói, nếu còn chỗ dưới trần
            float avail = (room.r.yMax - 1) - top;
            if (avail >= 1.6f)
            {
                float rh = Mathf.Min(avail - 0.2f, hs.w * 0.5f);
                PutFit(hT, "f5_gable", cx, top - 0.15f, hs.w + 1.2f, rh, -30, tint);                       // mái hồi gỗ (có mặt tam giác)
                if (upper) PutFit(hT, "f5_gableSlate", cx, top - 0.15f, hs.w + 1.2f, rh, -29, tint);        // phố trên: phủ ngói đá lên, mặt tam giác lấy từ lớp dưới
                if (avail >= 3.2f && idx % 2 == 1) Put(hT, upper ? "f5_chimneySlate" : "f5_chimney", hs.x0 + hs.w * 0.75f, top + rh * 0.35f, -32, tint);
            }

            // cửa ở tầng trệt, cửa sổ ở các tầng trên
            bool stone = wall == 2;
            float doorX = (idx % 2 == 0) ? hs.x0 + 2.5f : hs.x0 + hs.w - 2.5f;
            Put(hT, stone ? "f5_doorArch" : "f5_doorWood", doorX, hs.y0, -28, tint);
            if (hs.w >= 10) Put(hT, stone ? "f5_winArch" : "f5_winMid", (idx % 2 == 0) ? hs.x0 + hs.w - 3f : hs.x0 + 3f, hs.y0 + 1.2f, -28, tint);
            for (int k = 1; k < hs.floors; k++)
            {
                float wy = hs.y0 + 5 * k + 1.0f;
                string win = stone ? "f5_winArch" : (k == hs.floors - 1 && !upper ? "f5_winFlower" : "f5_winBig");
                if (hs.w >= 11) { Put(hT, win, hs.x0 + hs.w * 0.28f, wy, -28, tint); Put(hT, win, hs.x0 + hs.w * 0.72f, wy, -28, tint); }
                else Put(hT, win, cx, wy, -28, tint);
            }
            idx++;
        }
    }

    static void P(Transform t, string room, string n, float dx, float dy = 0, bool flip = false, float s = 1, int order = -5, Color? tint = null)
        => Put(t, n, rooms[room].r.xMin + dx, rooms[room].Floor + dy, order, tint ?? Color.white, s, s, flip);

    static void BuildStreetProps()
    {
        var t = new GameObject("Props").transform; t.SetParent(artRoot);
        var torches = new GameObject("Torches").transform; torches.SetParent(artRoot);
        void T(string room, float dx, float dy = 0) => Torch(torches, rooms[room].r.xMin + dx, rooms[room].Floor + dy, (dx * 0.37f) % 1f);

        // ---- Khu A: trại orc tạm bợ ----
        P(t, "R02", "f5_fence", 5); P(t, "R02", "f5_fence", 7); P(t, "R02", "f5_fence", 9, 0, true); T("R02", 12); T("R02", 24);
        P(t, "R03", "f5_bunting", 22, 7.6f); P(t, "R03", "f5_bunting", 48, 7.6f, true);
        P(t, "R03", "f5_crateStack", 15); P(t, "R03", "f5_barrel", 30); P(t, "R03", "f5_crate", 32.2f); P(t, "R03", "f5_cart", 45);
        P(t, "R03", "s_basket", 26, 3.1f); P(t, "R03", "s_pot1", 38, 3.1f); P(t, "R03", "f5_barrel2", 57); T("R03", 2); T("R03", 70);
        P(t, "R04", "s_clothes", 9); P(t, "R04", "s_clothes", 25, 0, true); P(t, "R04", "f5_plank", 32);
        P(t, "R05", "s_hay", 14); P(t, "R05", "s_stump", 29); P(t, "R05", "s_clothes", 44); P(t, "R05", "f5_barrel", 58);
        P(t, "R05", "f5_barrel2", 60.3f); P(t, "R05", "f5_crate", 67); P(t, "R05", "s_hay", 74, 0, true);
        for (int i = 0; i < 4; i++) P(t, "R05", "f5_fence", 77 + 2 * i, 0, i % 2 == 1);   // rào chắn tạm quanh sàn nứt lớn
        T("R05", 2); T("R05", 46); T("R05", 66);
        P(t, "R06", "f5_fence", 4); P(t, "R06", "f5_plank", 13, 5); T("R06", 3);
        P(t, "R07", "f5_crateStack", 10); P(t, "R07", "f5_barrel", 20); P(t, "R07", "f5_crate", 34, 0, false, 0.8f); P(t, "R07", "s_rubble3", 44);
        P(t, "R08", "g_webL", 3, 5.5f); P(t, "R08", "g_webR", 46, 5.4f); P(t, "R08", "s_rubbleBig", 18); P(t, "R08", "f5_crate", 30, 0, false, 0.7f); P(t, "R08", "s_bottle1", 40);

        // ---- Khu B: phố trên ----
        P(t, "R09", "g_chain", 9, 12);
        P(t, "R10", "g_banner1", 16.5f, 11.5f); P(t, "R10", "g_banner2", 13.5f, 11.5f); P(t, "R10", "s_bench", 30);
        P(t, "R10", "f5_lamp", 35, 5); P(t, "R10", "s_rubble2", 52); P(t, "R10", "f5_barrel2", 70);
        P(t, "R11", "g_chain", 10, 14); P(t, "R11", "g_chain", 13, 20);
        P(t, "R12", "g_fireplace0", 7.5f, 4f, false, 1, -6); P(t, "R12", "g_stoneTable", 14.5f); P(t, "R12", "g_halberds", 17.5f, 0.2f);
        P(t, "R12", "s_sign", 33); P(t, "R12", "f5_barrel", 49); P(t, "R12", "s_rubble4", 68);
        P(t, "R13", "s_sign", 5f); P(t, "R13", "g_barrel", 9); P(t, "R13", "g_barrelSide", 11); P(t, "R13", "g_bench", 22);
        P(t, "R13", "g_candelabra", 18, 8.5f); P(t, "R13", "g_bench", 10, 5); P(t, "R13", "s_bottle2", 26, 5); P(t, "R13", "s_bottle3", 27, 5);
        P(t, "R13", "g_fireplace0", 24, 10, false, 1, -6); P(t, "R13", "f5_planter", 52);
        P(t, "R14", "g_barrelSide", 4); P(t, "R14", "g_barrelSide", 6); P(t, "R14", "g_barrel", 8); P(t, "R14", "g_barrelSide", 18);
        P(t, "R14", "g_barrelSide", 20); P(t, "R14", "g_barrel", 28); P(t, "R14", "f5_crate", 31, 0, false, 0.7f);
        P(t, "R15", "s_well", 7); P(t, "R15", "s_wagon", 27, 0, true); P(t, "R15", "s_rubbleBig", 40); P(t, "R15", "s_rubble2", 66.5f);
        P(t, "R15", "f5_cart", 43, 0, true, 0.8f);   // dời khỏi miệng hầm R13|R15 P(t, "R15", "s_rubble3", 24);

        // ---- Khu C: phố dưới ----
        T("R16", 4, 1); T("R16", 13, 9);
        P(t, "R17", "s_pot1", 17); P(t, "R17", "f5_planter", 33, 0, false, 0.7f); P(t, "R17", "s_pot2", 48); P(t, "R17", "g_bookshelf1", 8, 5); P(t, "R17", "g_fireplaceOff", 26, 5);
        P(t, "R18", "s_cartSmall", 14); P(t, "R18", "s_basket", 30); P(t, "R18", "s_rubble2", 46); P(t, "R18", "f5_barrel", 50);
        P(t, "R19", "s_rubbleBig", 36); P(t, "R19", "s_rubble3", 52); P(t, "R19", "s_bench", 16); P(t, "R19", "s_sign", 68); P(t, "R19", "s_rubble1", 85);
        P(t, "R20", "g_gothicWindow", 10, 2, false, 1, -6); P(t, "R20", "g_gothicWindow", 42, 2, false, 1, -6);
        P(t, "R20", "g_statue1", 20); P(t, "R20", "g_statue2", 33); P(t, "R20", "g_floorCandles", 27, 1); P(t, "R20", "s_rubbleBig", 48);
        P(t, "R21", "g_chain", 9, 18); P(t, "R21", "g_chain", 13, 8);

        // ---- Khu D: mê cung tối. Ít đồ, mỗi phòng một "dấu hiệu" riêng để người chơi nhận ra mình đang ở đâu trong bóng tối ----
        P(t, "R22", "g_webR", 27, 15.5f); P(t, "R22", "s_rubble2", 8); P(t, "R22", "g_chain", 20, 12);
        P(t, "R23", "g_chain", 22, 5); P(t, "R23", "g_chain", 56, 5); P(t, "R23", "s_rubble3", 40);                 // tiếng vọng: xích treo
        P(t, "R24", "s_rubble1", 8); P(t, "R24", "s_rubble2", 40); P(t, "R24", "s_bottle3", 44);                    // ngõ chuột
        P(t, "R25", "g_coffinA", 8); P(t, "R25", "g_coffinB", 24); P(t, "R25", "g_coffinA", 40, 0, true); P(t, "R25", "g_coffinB", 52, 0, true);
        P(t, "R26", "g_webL", 3, 5.5f); P(t, "R26", "g_webR", 58, 5.4f);
        P(t, "R28", "s_rubbleBig", 8); P(t, "R28", "s_rubble4", 60); P(t, "R28", "g_chain", 34, 12);
        P(t, "R29", "g_chain", 5, 22); P(t, "R29", "g_chain", 13, 12);
        P(t, "R30", "g_chain", 12, 12);
        P(t, "R31", "g_coffinA", 10); P(t, "R31", "s_rubble3", 28); P(t, "R31", "g_coffinB", 54); P(t, "R31", "s_rubble2", 66); // hầm xương
        P(t, "R32", "g_webL", 3, 5.5f); P(t, "R32", "g_webR", 22, 5.4f); P(t, "R32", "g_webL", 14, 5.5f, true);      // ổ nhện
        P(t, "R33", "g_chain", 9, 12); P(t, "R33", "g_chain", 27, 14);
        P(t, "R34", "g_statue1", 10); P(t, "R34", "g_statue2", 28); P(t, "R34", "g_statue1", 44); P(t, "R34", "g_statue2", 60); P(t, "R34", "g_statue1", 74); // hành lang gương: tượng giống nhau
        P(t, "R35", "s_rubbleBig", 12); P(t, "R35", "s_rubble1", 48);
        P(t, "R36", "g_chain", 10, 5); P(t, "R36", "s_rubble4", 38);
        P(t, "R37", "g_coffinA", 10); P(t, "R37", "g_coffinB", 26); P(t, "R37", "g_coffinA", 42); P(t, "R37", "g_coffinB", 58); P(t, "R37", "g_coffinA", 76, 0, true); P(t, "R37", "g_coffinB", 92, 0, true);
        P(t, "R38", "g_floorCandles", 21); P(t, "R38", "g_floorCandles", 33);
        P(t, "R39", "s_rubbleBig", 22); P(t, "R39", "g_chain", 40, 5); P(t, "R39", "s_rubble3", 70); P(t, "R39", "g_chain", 86, 5);
        P(t, "R40", "s_rubble2", 10); P(t, "R40", "s_bottle1", 28);
        P(t, "R41", "g_chain", 14, 30); P(t, "R41", "g_chain", 9, 14);

        // ---- Khu E ----
        T("R42", 3); T("R42", 29); P(t, "R42", "s_rubble1", 18);
        P(t, "R43", "g_pillar", 6); P(t, "R43", "g_pillar", 46);
    }

    [MenuItem("Tools/Culinaria/F5 - Xuất ảnh lưới tile (debug)")]
    public static void ExportLayoutPng()
    {
        var ground = GameObject.Find("Grid_F5/F5_Ground")?.GetComponent<Tilemap>();
        var platTm = GameObject.Find("Grid_F5/F5_Platform_OneWay (sàn tầng nhà, bậc)")?.GetComponent<Tilemap>();
        if (ground == null) { Debug.Log("[F5] Chưa dựng tầng 5."); return; }
        var tex = new Texture2D(W, H);
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
        {
            var p = new Vector3Int(i + minX, j + minY, 0);
            Color c = ground.HasTile(p) ? ground.GetColor(p) * 0.6f : platTm != null && platTm.HasTile(p) ? new Color(1f, 0.8f, 0.3f) : new Color(0.1f, 0.12f, 0.16f);
            c.a = 1; tex.SetPixel(i, j, c);
        }
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            Color? mk = t.name.StartsWith("F5_Chest") ? Color.yellow : t.name.StartsWith("Brazier_") ? new Color(1f, 0.4f, 0.1f) :
                        t.name.Contains("BreakableFloor") ? Color.magenta : t.name.StartsWith("Gate_") || t.name.StartsWith("FakeWall") ? new Color(1f, 0.5f, 0.6f) : (Color?)null;
            if (mk == null) continue;
            int px = Mathf.FloorToInt(t.position.x) - minX, py = Mathf.FloorToInt(t.position.y) - minY;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (px + dx >= 0 && px + dx < W && py + dy >= 0 && py + dy < H) tex.SetPixel(px + dx, py + dy, mk.Value);
        }
        string path = "Assets/Editor/_tmp_F5_layout.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("[F5] Đã xuất " + path + " (xoá được, chỉ để kiểm tra)");
    }

    static void BuildNpcPlaceholders()
    {
        if (!spr.ContainsKey("idle-01")) { log.Add("Không thấy sprite Character/idle-01: bỏ NPC tạm."); return; }
        var t = new GameObject("NPC_Orc_TamThoi (đổi màu từ Character, thay bằng sprite orc sau)").transform; t.SetParent(artRoot);
        float k = spr["idle-01"].pixelsPerUnit / 16f;                                         // đưa về thang 16 px = 1 ô
        var orc = new Color(0.55f, 0.85f, 0.45f);
        void N(string label, string room, float dx, bool flip) { var sr = Put(t, "idle-01", rooms[room].r.xMin + dx, rooms[room].Floor, -3, orc, k, k, flip); if (sr) sr.name = label; }
        N("Orc_LinhGac", "R02", 18, true);
        N("Orc_ThuongNhan", "R03", 30, false);
        N("Orc_GacNapHam", "R03", 9, true);
        N("Orc_TocTruong", "R05", 10, false);
        N("Orc_Dan_1", "R05", 40, true);
        N("Orc_Dan_2", "R05", 53, false);
    }

    // ===================== FOG OF WAR CHO KHU MÊ CUNG =====================
    // Hệ fog sẵn có của scene: FogOfWarCamera (không xoá → nhớ chỗ đã đi, kênh đỏ), FogOfWarLayer2Camera (xoá mỗi khung →
    // vùng đang nhìn, kênh xanh), shader Hidden/FogOfWar ghép hai lớp lên RawImage của FogOfWarCanvas (World Space).
    // Bản cũ cố định quanh gốc (0,0), phủ 76×76 ô. Ở đây đặt lại cho khớp khu mê cung và dùng render texture riêng của tầng 5.
    const float FogCX = 144f, FogCY = -108f, FogW = 288f, FogH = 72f;   // phủ cột 0–15, hàng 8–13 (y -72 .. -144)

    static void BuildFogRig(UnityEngine.SceneManagement.Scene scene)
    {
        Transform Find(string n)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }
        var root = Find("______FogOfWar_____");
        var camExplored = Find("FogOfWarCamera")?.GetComponent<Camera>();
        var camView = Find("FogOfWarLayer2Camera")?.GetComponent<Camera>();
        var canvas = Find("FogOfWarCanvas")?.GetComponent<Canvas>();
        var mainCircle = Find("FogOfWarMainCircle")?.GetComponent<SpriteRenderer>();
        var viewCircle = Find("FogOfWarLayer2Circle")?.GetComponent<SpriteRenderer>();
        if (root == null || camExplored == null || camView == null || canvas == null || mainCircle == null || viewCircle == null)
        { log.Add("Thiếu một phần hệ fog (camera/canvas/vòng sáng): bỏ qua đặt lại fog."); return; }

        RenderTexture RT(string n)
        {
            string path = ArtDir + "/" + n + ".renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (rt == null) { rt = new RenderTexture(1024, 256, 0, RenderTextureFormat.ARGB32) { name = n, filterMode = FilterMode.Bilinear }; AssetDatabase.CreateAsset(rt, path); }
            return rt;
        }
        var explored = RT("F5_FogExplored"); var view = RT("F5_FogView");
        string matPath = ArtDir + "/F5_FogOfWar.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { var sh = Shader.Find("Hidden/FogOfWar"); if (sh == null) { log.Add("Không thấy shader Hidden/FogOfWar"); return; } mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_MainTex", explored); mat.SetTexture("_SecondaryTex", view); EditorUtility.SetDirty(mat);

        foreach (var (cam, rt) in new[] { (camExplored, explored), (camView, view) })
        {
            cam.targetTexture = rt;
            cam.orthographic = true; cam.orthographicSize = FogH / 2f;     // RT 4:1 → rộng 288, cao 72
            cam.transform.position = new Vector3(FogCX, FogCY, -10);
            EditorUtility.SetDirty(cam);
        }
        var crt = canvas.GetComponent<RectTransform>();
        crt.position = new Vector3(FogCX, FogCY, 0); crt.localScale = Vector3.one; crt.sizeDelta = new Vector2(FogW, FogH);
        canvas.sortingOrder = 300;
        foreach (var img in canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>(true))
        {
            img.texture = explored; img.material = mat;
            var r = img.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(FogW, FogH); r.localScale = Vector3.one;
            EditorUtility.SetDirty(img);
        }
        // AutoBindFogOfWar tự Destroy vòng sáng khi chạm tag Enemy → trong mê cung đầy quái, vòng sáng sẽ mất. Tắt collider (không xoá).
        foreach (var c in new[] { mainCircle, viewCircle })
        {
            var col = c.GetComponent<CircleCollider2D>();
            if (col != null && col.enabled) { col.enabled = false; EditorUtility.SetDirty(col); log.Add("Tắt CircleCollider2D trên " + c.name + " (tránh bị Destroy khi chạm quái)."); }
        }
        var rig = root.GetComponent<F5FogRig>() ?? root.gameObject.AddComponent<F5FogRig>();
        var so = new SerializedObject(rig); so.FindProperty("explored").objectReferenceValue = explored; so.ApplyModifiedPropertiesWithoutUndo();
        log.Add($"Fog of war: phủ khu mê cung {FogW}×{FogH} ô quanh ({FogCX}, {FogCY}), render texture riêng F5_FogExplored / F5_FogView.");

        BuildBraziers(mainCircle, viewCircle);
    }

    /// Đèn thờ: chạm vào thì thắp. Khi thắp, một vùng tròn quanh đèn luôn sáng (vẽ lên cả hai lớp fog), làm mốc định hướng.
    static void BuildBraziers(SpriteRenderer mainCircle, SpriteRenderer viewCircle)
    {
        var parent = new GameObject("Braziers (đèn thờ)").transform; parent.SetParent(artRoot);
        int layExplored = mainCircle.gameObject.layer, layView = viewCircle.gameObject.layer;
        foreach (var (room, dx, label) in new[] { ("R27", 4.5f, "DenTho_Chinh"), ("R38", 27f, "DaiSanhCot"), ("R36", 24f, "CongXoay") })
        {
            var rm = rooms[room];
            float x = rm.r.xMin + dx, y = rm.Floor;
            var go = new GameObject($"Brazier_{room}_{label}"); go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, y + 1.5f, 0);
            var bc = go.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = new Vector2(2.5f, 3f);

            var unlit = Put(go.transform, "g_torch0", x, y, -4, new Color(0.35f, 0.35f, 0.4f)); if (unlit) unlit.name = "Unlit";
            var lit = Torch(go.transform, x, y, 0.3f); if (lit) lit.name = "Lit";
            GameObject Reveal(string n, SpriteRenderer src, int layer)
            {
                var r = new GameObject(n); r.transform.SetParent(go.transform); r.layer = layer;
                r.transform.position = new Vector3(x, y + 1.5f, 0); r.transform.localScale = new Vector3(14, 14, 1);
                var sr = r.AddComponent<SpriteRenderer>(); sr.sprite = src.sprite; sr.color = src.color; sr.sharedMaterial = src.sharedMaterial;
                return r;
            }
            var rE = Reveal("Reveal_Explored", mainCircle, layExplored);
            var rV = Reveal("Reveal_View", viewCircle, layView);
            rE.SetActive(false); rV.SetActive(false); if (lit) lit.SetActive(false);   // trạng thái tắt; F5Brazier bật khi Player chạm

            var b = go.AddComponent<F5Brazier>();
            var so = new SerializedObject(b);
            so.FindProperty("litVisual").objectReferenceValue = lit;
            so.FindProperty("unlitVisual").objectReferenceValue = unlit ? unlit.gameObject : null;
            var rv = so.FindProperty("reveal"); rv.arraySize = 2;
            rv.GetArrayElementAtIndex(0).objectReferenceValue = rE; rv.GetArrayElementAtIndex(1).objectReferenceValue = rV;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
