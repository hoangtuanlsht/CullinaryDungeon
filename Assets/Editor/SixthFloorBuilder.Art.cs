using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// [Tầng 6] Phần hình ảnh của SixthFloorBuilder: thay khối màu greybox bằng tile và sprite có sẵn trong project.
/// - Hang (khu A, B, D): tile hang của tầng 3 (Assets/TileMap/F3Cave/caveA_*; CHỈ ĐỌC), tô ấm theo khu.
/// - Lâu đài (khu C): tile gạch đá của tầng 5 (F5_Art/Tiles/T_castleB_*; CHỈ ĐỌC) + một lớp tường nền không va chạm.
/// - Dung nham: tile dung nham của dungeontilesetfull (như tầng 3).
/// - Đồ trong lâu đài, đuốc: tileset.png (g_*, cắt sẵn; chỉ đọc). Gạch đổ, thùng: bản sao của tầng 5 (s_*, f5_*; chỉ đọc).
/// - Nền xa: BẢN SAO của Resouces/Background/background1.png (nền đỏ), Parallax_Backgrounds_Cave/3.png (bóng cột đá) và các ảnh cảnh
///   Firepious (hellwake, underfire, ossuary, pyroclast) trong Assets/BackGround/SixthFloor/F6_Art, import riêng, không đụng ảnh gốc.
/// Mọi sprite nằm dưới "______Art_F6_____" và được dựng lại mỗi lần chạy builder.
public static partial class SixthFloorBuilder
{
    const string ArtDir = "Assets/BackGround/SixthFloor/F6_Art";
    const string CaveDir = "Assets/TileMap/F3Cave/";
    const string CastleDir = "Assets/BackGround/FifthFloor/F5_Art/Tiles/T_";
    const string LavaTilePath = "Assets/TileMap/dungeontilesetfull 1_";
    const string SrcInterior = "Assets/Resouces/tileset.png";
    const string SrcFarRed = "Assets/Resouces/Background/background1.png";
    const string SrcLavaRock = "Assets/Resouces/LavaTile set.png";                       // đá nứt dung nham, lưới 32 px (bạn thêm 13/11/2026)
    const string SrcCaveCols = "Assets/Resouces/Parallax_Backgrounds_Cave/3.png";        // bóng cột đá + măng đá (384×216, nền trong suốt)
    static readonly string[] OptionalSpriteSheets = { "Assets/BackGround/FifthFloor/F5_Art/F5_StreetProps.png", "Assets/BackGround/FifthFloor/F5_Art/F5_MedievalProps.png" };

    static Dictionary<string, Sprite> spr;
    static Transform artRoot;
    static TileBase[] cSurf, cUnder, cCeil, cAboveCeil, cDeep, kTop, kFill, lavaTop;
    static TileBase beam;
    static Sprite farRed, caveCols;

    /// Ảnh cảnh nền (bộ Firepious 256×144 trong Assets/Resouces/<bộ>/1x; bạn thêm 13/11/2026). Một ảnh = đúng một khung 18×10 ô.
    /// (phòng, ô bắt đầu cx/cy tính từ góc trên-trái phòng, k = ảnh phủ k×k khung, n = số lần lặp ngang (lật gương xen kẽ để liền mép), ảnh)
    static readonly (string room, int cx, int cy, int k, int n, string img)[] Scenes =
    {
        ("R02", 0, 0, 1, 3, "hellwake/01_hollowfall"),   ("R03", 0, 0, 2, 1, "underfire/03_the_deep"),      // thác dung nham
        ("R04", 0, 0, 1, 3, "hellwake/02_emberflow"),    ("R05", 0, 0, 2, 1, "hellwake/03_cindervault"),    // trại thám hiểm
        ("R07", 0, 0, 1, 3, "hellwake/01_hollowfall"),   ("R08", 0, 0, 1, 3, "hellwake/02_emberflow"),
        ("R09", 0, 0, 2, 1, "hellwake/01_hollowfall"),   ("R10", 1, 0, 1, 1, "underfire/01_the_altar"),     // phòng mốc bờ hồ
        ("R12", 0, 0, 1, 2, "underfire/02_the_chamber"), ("R14", 0, 0, 1, 2, "pyroclast/04_the_cooling"),   // cầu đá, tường thành
        ("R15", 0, 0, 2, 2, "underfire/04_the_hall"),    ("R16", 0, 0, 1, 2, "ossuary/02_the_gallery"),     // đại sảnh, hành lang tranh
        ("R20", 0, 0, 2, 1, "underfire/02_the_chamber"), ("R21", 0, 0, 1, 1, "pyroclast/02_the_fury"),      // nhà nguyện, tháp canh
        ("R22", 0, 0, 1, 3, "ossuary/03_the_ossuary"),   ("R23", 0, 0, 1, 3, "hellwake/03_cindervault"),    // hầm mộ, ngục tối
        ("R24", 0, 0, 1, 3, "underfire/02_the_chamber"), ("R25", 0, 0, 2, 1, "ossuary/04_the_throne"),      // hành lang sụp, phòng ngai
        ("R27", 0, 0, 1, 1, "ossuary/01_the_door"),      ("R28", 1, 0, 2, 1, "hellwake/04_pyreheart"),      // tiền phòng, hang rồng
        // 13/11/2026: các phòng trước đó chưa có ảnh nên quá tối
        ("R11", 0, 0, 1, 2, "hellwake/02_emberflow"),    ("R17", 0, 0, 1, 2, "hellwake/03_cindervault"),    // hốc bí mật, bếp lâu đài
        ("R18", 0, 0, 2, 1, "ossuary/02_the_gallery"),   ("R29", 0, 0, 1, 2, "hellwake/04_pyreheart"),      // thư viện, kho báu rồng
        ("R13", 0, 1, 1, 1, "ossuary/01_the_door"),                                                          // tháp cổng (khung dưới)
    };
    static readonly Dictionary<string, Sprite> sceneSprites = new Dictionary<string, Sprite>();
    static readonly List<RectInt> sceneRects = new List<RectInt>();     // vùng đã có ảnh cảnh → không vẽ tường nền lên
    static TileBase[,] lr;                 // đá dung nham 3×3: [cột 0..2 = trái/giữa/phải, hàng 0..2 = trên/giữa/dưới]
    static TileBase[] lrCol, lrRow;        // cột rộng 1 ô (trên/giữa/dưới), dải cao 1 ô (trái/giữa/phải)
    static TileBase lrSingle;
    static bool lrOk;
    static readonly HashSet<string> CastleRooms = new HashSet<string>(Enumerable.Range(13, 13).Select(n => "R" + n));   // R13..R25

    // ===================== CHUẨN BỊ TÀI NGUYÊN =====================
    static TileBase LoadTile(string path, List<string> missing)
    {
        var t = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        if (t == null) missing.Add(path);
        return t;
    }

    static bool PrepareArt()
    {
        var missing = new List<string>();
        TileBase[] Row(int r) => Enumerable.Range(59, 10).Select(c => LoadTile($"{CaveDir}caveA_{c}_{r}.asset", missing)).ToArray();
        cSurf = Row(61); cUnder = Row(62); cAboveCeil = Row(77); cCeil = Row(78);
        cDeep = new[] { "45_63", "46_63", "45_64", "46_64" }.Select(n => LoadTile($"{CaveDir}caveA_{n}.asset", missing)).ToArray();
        kTop = new[] { "castleB_23_1", "castleB_24_1", "castleB_25_1" }.Select(n => LoadTile(CastleDir + n + ".asset", missing)).ToArray();
        kFill = new[] { "castleB_24_2", "castleB_24_2", "castleB_23_2", "castleB_25_2", "castleB_18_3" }.Select(n => LoadTile(CastleDir + n + ".asset", missing)).ToArray();
        beam = LoadTile(CastleDir + "castleB_2_6.asset", missing);
        lavaTop = new[] { "298", "299" }.Select(n => LoadTile(LavaTilePath + n + ".asset", missing)).ToArray();
        if (missing.Count > 0) { log.Add($"THIẾU {missing.Count} tile (vd {missing[0]}) → giữ greybox màu."); return false; }
        if (!System.IO.File.Exists(SrcInterior)) { log.Add("THIẾU " + SrcInterior + " → giữ greybox màu."); return false; }

        spr = new Dictionary<string, Sprite>();
        foreach (var path in new[] { SrcInterior }.Concat(OptionalSpriteSheets))
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()) spr[s.name] = s;

        if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/BackGround/SixthFloor", "F6_Art");
        farRed = CopySingle(SrcFarRed, "F6_BG_FarRed.png", 16);
        sceneSprites.Clear(); sceneRects.Clear();
        foreach (var sc in Scenes)
        {
            if (!sceneSprites.ContainsKey(sc.img))
                sceneSprites[sc.img] = CopySingle("Assets/Resouces/" + sc.img.Replace("/", "/1x/") + ".png", "F6_Scene_" + sc.img.Replace("/", "_") + ".png", 14.4f, true);
            if (sceneSprites[sc.img] == null) continue;
            var r = rooms[sc.room].r;
            sceneRects.Add(new RectInt(r.xMin + sc.cx * SW, r.yMax - (sc.cy + sc.k) * SH, sc.k * sc.n * SW, sc.k * SH));
        }
        caveCols = CopySingle(SrcCaveCols, "F6_BG_CotDa.png", 21.6f);       // 216 px = 10 ô = đúng một khung phòng
        lrOk = PrepareLavaRock();
        return true;
    }

    /// Chép file ảnh (không chép .meta) vào F6_Art và import thành 1 sprite: Point, không nén, FullRect, lặp được.
    static Sprite CopySingle(string src, string fileName, float ppu, bool center = false)
    {
        if (!System.IO.File.Exists(src)) { log.Add("Không có ảnh nền " + src + " (bỏ qua)."); return null; }
        string dst = ArtDir + "/" + fileName;
        if (!System.IO.File.Exists(dst))
        {
            System.IO.File.Copy(src, dst);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            log.Add("Chép " + src + " → " + dst);
        }
        var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
        var set = new TextureImporterSettings(); ti.ReadTextureSettings(set);
        if (set.textureType != TextureImporterType.Sprite || set.spritePixelsPerUnit != ppu || set.spriteMode != (int)SpriteImportMode.Single
            || set.wrapMode != TextureWrapMode.Repeat || set.spriteMeshType != SpriteMeshType.FullRect || set.filterMode != FilterMode.Point
            || set.spriteAlignment != (int)(center ? SpriteAlignment.Center : SpriteAlignment.BottomLeft))
        {
            set.textureType = TextureImporterType.Sprite; set.spriteMode = (int)SpriteImportMode.Single;
            set.spritePixelsPerUnit = ppu; set.filterMode = FilterMode.Point; set.wrapMode = TextureWrapMode.Repeat;
            set.mipmapEnabled = false; set.alphaIsTransparency = true;
            set.spriteMeshType = SpriteMeshType.FullRect; set.spriteAlignment = (int)(center ? SpriteAlignment.Center : SpriteAlignment.BottomLeft);
            ti.SetTextureSettings(set);
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            log.Add("Import lại " + dst + " (Sprite, PPU " + ppu + ", Full Rect)");
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(dst);
    }

    /// Bộ đá dung nham 32 px: chép vào F6_Art, cắt theo lưới 32 px (PPU 32 → mỗi ô đúng 1 ô lưới), tạo Tile asset.
    /// Bố cục sheet (cột, hàng tính từ trên): khối 3×3 ở (0..2, 0..2); cột 1 ô ở (3, 0..2); ô đơn (4,1); dải ngang (4..6, 2).
    static bool PrepareLavaRock()
    {
        if (!System.IO.File.Exists(SrcLavaRock)) { log.Add("Không có " + SrcLavaRock + " → hang dùng tile hang tầng 3."); return false; }
        string dst = ArtDir + "/F6_LavaRock.png";
        if (!System.IO.File.Exists(dst))
        {
            System.IO.File.Copy(SrcLavaRock, dst);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            log.Add("Chép " + SrcLavaRock + " → " + dst);
        }
        var cells = new List<(int c, int r)>();
        for (int c = 0; c < 4; c++) for (int r = 0; r < 3; r++) cells.Add((c, r));
        cells.Add((4, 1)); cells.Add((4, 2)); cells.Add((5, 2)); cells.Add((6, 2));
        var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
#pragma warning disable 0618
        if (ti.spriteImportMode != SpriteImportMode.Multiple || ti.spritePixelsPerUnit != 32 || ti.spritesheet.Length != cells.Count || ti.filterMode != FilterMode.Point)
        {
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = 32; ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed; ti.alphaIsTransparency = true;
            ti.spritesheet = cells.Select(k => new SpriteMetaData
            {
                name = $"lr_{k.c}_{k.r}", rect = new Rect(k.c * 32, 288 - (k.r + 1) * 32, 32, 32), alignment = (int)SpriteAlignment.Center
            }).ToArray();
            ti.SaveAndReimport();
            log.Add("Cắt " + dst + " thành " + cells.Count + " ô 32 px");
        }
#pragma warning restore 0618
        var sprites = AssetDatabase.LoadAllAssetsAtPath(dst).OfType<Sprite>().ToDictionary(x => x.name, x => x);
        if (!AssetDatabase.IsValidFolder(ArtDir + "/Tiles")) AssetDatabase.CreateFolder(ArtDir, "Tiles");
        TileBase Make(int c, int r)
        {
            string n = $"lr_{c}_{r}";
            if (!sprites.TryGetValue(n, out var sp)) { log.Add("Thiếu sprite " + n); return null; }
            string tp = ArtDir + "/Tiles/T_" + n + ".asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(tp);
            if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, tp); }
            t.sprite = sp; t.colliderType = Tile.ColliderType.Grid; t.flags = TileFlags.None;
            EditorUtility.SetDirty(t);
            return t;
        }
        lr = new TileBase[3, 3];
        for (int c = 0; c < 3; c++) for (int r = 0; r < 3; r++) lr[c, r] = Make(c, r);
        lrCol = new[] { Make(3, 0), Make(3, 1), Make(3, 2) };
        lrRow = new[] { Make(4, 2), Make(5, 2), Make(6, 2) };
        lrSingle = Make(4, 1);
        return lr.Cast<TileBase>().Concat(lrCol).Concat(lrRow).Append(lrSingle).All(t => t != null);
    }

    // ===================== TILE ĐỊA HÌNH =====================
    static int Hash(int x, int y) { unchecked { int h = x * 73856093 ^ y * 19349663; return h < 0 ? -h : h; } }
    static bool Open(int x, int y) => In(x, y) && !Get(solid, x, y) && !Get(lava, x, y);

    static readonly Color TintA = new Color(1f, 0.80f, 0.72f);       // hang dung nham: ấm (tile hang tầng 3)
    static readonly Color TintB = new Color(0.86f, 0.70f, 0.82f);    // hồ dung nham: tím than
    static readonly Color TintC = new Color(0.82f, 0.82f, 0.98f);    // lâu đài: đá lạnh
    static readonly Color TintD = new Color(1f, 0.64f, 0.48f);       // hang rồng: than hồng
    static readonly Color RockA = Color.white;                        // đá dung nham giữ màu gốc
    static readonly Color RockB = new Color(0.84f, 0.76f, 0.92f);
    static readonly Color RockD = new Color(1f, 0.90f, 0.80f);

    static (TileBase tile, Color tint) GroundTileAt(int x, int y)
    {
        char z = ZoneAt(x, y);
        bool up = Open(x, y + 1), down = Open(x, y - 1);
        if (z == 'C')
            return (up ? kTop[((x % 3) + 3) % 3] : kFill[Hash(x, y) % kFill.Length], TintC);
        if (lrOk)
        {
            bool lf = Open(x - 1, y), rt = Open(x + 1, y);
            TileBase rock = up && down && lf && rt ? lrSingle
                          : lf && rt ? lrCol[up ? 0 : down ? 2 : 1]
                          : up && down ? lrRow[lf ? 0 : rt ? 2 : 1]
                          : lr[lf ? 0 : rt ? 2 : 1, up ? 0 : down ? 2 : 1];
            return (rock, z == 'A' ? RockA : z == 'B' ? RockB : RockD);
        }
        int ix = ((x % 10) + 10) % 10;
        TileBase t = up ? cSurf[ix]
                   : down ? cCeil[ix]
                   : Open(x, y + 2) || Open(x - 1, y) || Open(x + 1, y) ? cUnder[ix]
                   : Open(x, y - 2) ? cAboveCeil[ix]
                   : cDeep[(x & 1) + ((y & 1) == 0 ? 2 : 0)];
        return (t, z == 'A' ? TintA : z == 'B' ? TintB : TintD);
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
        for (int k = 0; k < ps.Count; k++) { tm.SetTileFlags(ps[k], TileFlags.None); tm.SetColor(ps[k], cs[k]); }
    }

    static void PaintPlatformArt(Tilemap tm)
    {
        var ps = new List<Vector3Int>();
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
            if (plat[i, j] && !solid[i, j]) ps.Add(new Vector3Int(i + minX, j + minY, 0));
        tm.SetTiles(ps.ToArray(), Enumerable.Repeat(beam, ps.Count).ToArray());
        foreach (var p in ps) { tm.SetTileFlags(p, TileFlags.None); tm.SetColor(p, ZoneAt(p.x, p.y) == 'C' ? new Color(0.85f, 0.85f, 0.92f) : new Color(1f, 0.86f, 0.74f)); }
    }

    static void PaintLavaArt(Tilemap tm)
    {
        var ps = new List<Vector3Int>(); var ts = new List<TileBase>();
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
            if (lava[i, j]) { ps.Add(new Vector3Int(i + minX, j + minY, 0)); ts.Add(lavaTop[(i + minX) & 1]); }
        tm.SetTiles(ps.ToArray(), ts.ToArray());
    }

    /// Tường nền lâu đài: phủ mọi ô trống trong các phòng lâu đài bằng gạch đá tô tối, không va chạm.
    static void PaintBackWallArt(Tilemap tm)
    {
        var ps = new List<Vector3Int>(); var ts = new List<TileBase>();
        foreach (var id in CastleRooms)
        {
            var r = rooms[id].r;
            for (int x = r.xMin; x < r.xMax; x++) for (int y = r.yMin - 1; y < r.yMax; y++)
                if (!Get(solid, x, y) && !sceneRects.Any(sr => sr.Contains(new Vector2Int(x, y)))) { ps.Add(new Vector3Int(x, y, 0)); ts.Add(kFill[Hash(x, y) % kFill.Length]); }
        }
        tm.SetTiles(ps.ToArray(), ts.ToArray());
        var dark = new Color(0.74f, 0.72f, 0.90f);     // tường nền lâu đài (13/11/2026: sáng hơn, bản trước quá tối)
        foreach (var p in ps) { tm.SetTileFlags(p, TileFlags.None); tm.SetColor(p, dark); }
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
    static readonly Color Cold = new Color(0.80f, 0.80f, 0.92f);

    static void DecorateF6(UnityEngine.SceneManagement.Scene scene)
    {
        artRoot = new GameObject("______Art_F6_____").transform;
        BuildBackdrop();
        BuildProps();
        log.Add($"Hình ảnh: {artRoot.GetComponentsInChildren<SpriteRenderer>().Length} sprite.");
    }

    static void BuildBackdrop()
    {
        var bd = new GameObject("Backdrop").transform; bd.SetParent(artRoot);
        foreach (var kv in rooms)
        {
            var r = kv.Value.r; char z = ZoneAt(r.xMin + 2, r.yMin + 2);
            // Dải màu nền của phòng (luôn có, để camera không bao giờ lộ khoảng trống)
            var band = new GameObject("Band_" + kv.Key); band.transform.SetParent(bd);
            band.transform.position = new Vector3(r.center.x, r.center.y - 0.5f, 0);
            Vector2 ss = blockSprite.bounds.size;
            band.transform.localScale = new Vector3(r.width / ss.x, (r.height + 1) / ss.y, 1);
            var bsr = band.AddComponent<SpriteRenderer>(); bsr.sprite = blockSprite; bsr.sortingOrder = -100;
            bsr.color = z == 'A' ? new Color(0.14f, 0.06f, 0.06f) : z == 'B' ? new Color(0.12f, 0.06f, 0.11f)
                      : z == 'C' ? new Color(0.10f, 0.10f, 0.16f) : new Color(0.16f, 0.07f, 0.05f);

            // Nền xa đỏ, lặp theo cả hai chiều, cho hang và hang rồng
            if (z == 'C' || farRed == null) continue;
            var far = new GameObject("FarRed_" + kv.Key); far.transform.SetParent(bd);
            far.transform.position = new Vector3(r.xMin, r.yMin - 1, 0);
            var fsr = far.AddComponent<SpriteRenderer>();
            fsr.sprite = farRed; fsr.drawMode = SpriteDrawMode.Tiled; fsr.tileMode = SpriteTileMode.Continuous;
            fsr.size = new Vector2(r.width, r.height + 1); fsr.sortingOrder = -95;
            fsr.color = z == 'A' ? new Color(0.46f, 0.33f, 0.32f) : z == 'B' ? new Color(0.40f, 0.29f, 0.40f) : new Color(0.62f, 0.38f, 0.30f);
            // Lớp giữa: bóng cột đá và măng đá (đỏ sẫm). Ảnh cao đúng 10 ô; phòng cao hơn thì phóng đều cho vừa chiều cao phòng.
            if (caveCols == null) continue;
            float k = r.height / 10f;
            var cols = new GameObject("CaveCols_" + kv.Key); cols.transform.SetParent(bd);
            cols.transform.position = new Vector3(r.xMin, r.yMin, 0);
            cols.transform.localScale = new Vector3(k, k, 1);
            var csr = cols.AddComponent<SpriteRenderer>();
            csr.sprite = caveCols; csr.drawMode = SpriteDrawMode.Tiled; csr.tileMode = SpriteTileMode.Continuous;
            csr.size = new Vector2(r.width / k, 10f); csr.sortingOrder = -93;
            csr.color = z == 'D' ? new Color(1f, 0.55f, 0.42f) : new Color(0.85f, 0.45f, 0.48f);
        }

        // Ảnh cảnh nền: mỗi ảnh phủ k×k khung, kéo ngang 1,25% cho khít đúng 18 ô; các bản lặp lật gương xen kẽ để mép liền nhau.
        foreach (var sc in Scenes)
        {
            if (!sceneSprites.TryGetValue(sc.img, out var sp) || sp == null) continue;
            var r = rooms[sc.room].r; char z = ZoneAt(r.xMin + 2, r.yMin + 2);
            float cellW = SW * sc.k, cellH = SH * sc.k;
            for (int i = 0; i < sc.n; i++)
            {
                var go = new GameObject($"Scene_{sc.room}_{i}_{sc.img.Replace('/', '_')}"); go.transform.SetParent(bd);
                go.transform.position = new Vector3(r.xMin + sc.cx * SW + cellW * (i + 0.5f), r.yMax - sc.cy * SH - cellH / 2f, 0);
                go.transform.localScale = new Vector3(cellW / sp.bounds.size.x, cellH / sp.bounds.size.y, 1);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingOrder = -88; sr.flipX = i % 2 == 1;
                sr.color = z == 'C' ? new Color(0.72f, 0.72f, 0.78f) : z == 'D' ? new Color(0.82f, 0.78f, 0.76f) : new Color(0.66f, 0.63f, 0.63f);
            }
        }
    }

    static void P(Transform t, string room, string n, float dx, float dy = 0, bool flip = false, float s = 1, int order = -5, Color? tint = null)
        => Put(t, n, rooms[room].r.xMin + dx, rooms[room].Floor + dy, order, tint ?? Cold, s, s, flip);

    static void BuildProps()
    {
        var t = new GameObject("Props").transform; t.SetParent(artRoot);
        var torches = new GameObject("Torches").transform; torches.SetParent(artRoot);
        void T(string room, float dx, float dy = 0) => Torch(torches, rooms[room].r.xMin + dx, rooms[room].Floor + dy, (dx * 0.37f) % 1f);
        Color warm = new Color(1f, 0.86f, 0.78f);

        // ---- Khu A: hang dung nham (project chưa có đồ trang trí hang nóng → chỉ có trại và vài mốc) ----
        T("R01", 13); T("R02", 8); T("R02", 44);
        P(t, "R05", "f5_crate", 8, 0, false, 0.8f, -5, warm); P(t, "R05", "f5_barrel", 10.5f, 0, false, 1, -5, warm);
        P(t, "R05", "s_hay", 26, 0, false, 1, -5, warm); P(t, "R05", "f5_barrel2", 29.5f, 0, false, 1, -5, warm); T("R05", 6); T("R05", 30);
        P(t, "R07", "s_rubble3", 22, 0, false, 1, -5, warm); P(t, "R09", "s_rubbleBig", 24, 0, false, 1, -5, warm);
        P(t, "R12", "s_rubble2", 17, 0, false, 1, -5, warm);

        // ---- Khu C: lâu đài bỏ hoang ----
        P(t, "R13", "g_statue1", 4); P(t, "R13", "g_banner1", 12, 5); T("R13", 6, 1);                       // tháp cổng
        T("R14", 6); T("R14", 27);                                                                            // tường thành
        foreach (float px in new[] { 18f, 34f, 50f }) P(t, "R15", "g_pillar", px, 0, false, 1, -7);           // đại sảnh
        P(t, "R15", "g_gothicWindow", 13, 3, false, 1, -8); P(t, "R15", "g_gothicWindow", 44, 3, false, 1, -8);
        P(t, "R15", "g_fireplace0", 60, 0, false, 1, -6); P(t, "R15", "g_banner1", 30, 12); P(t, "R15", "g_banner2", 38, 12);
        P(t, "R15", "g_candelabra", 44, 7.5f); P(t, "R15", "g_candelabra", 56, 7.5f); T("R15", 5); T("R15", 66);
        P(t, "R16", "g_painting", 8, 3); P(t, "R16", "g_painting", 17, 3); P(t, "R16", "g_painting", 26, 3); T("R16", 21.5f);   // hành lang tranh
        P(t, "R17", "g_fireplace0", 8, 0, false, 1, -6); P(t, "R17", "g_barrel", 22); P(t, "R17", "g_barrelSide", 24);            // bếp
        P(t, "R17", "g_barrel", 26); P(t, "R17", "g_stoneTable", 30);
        P(t, "R18", "g_bookshelf0", 14, 0, false, 1, -6); P(t, "R18", "g_bookshelf1", 18, 0, false, 1, -6);                      // thư viện
        P(t, "R18", "g_bookshelf2", 22, 0, false, 1, -6); P(t, "R18", "g_bookshelf3", 31, 0, false, 1, -6);
        P(t, "R18", "g_bookshelf1", 27, 4, false, 1, -6); P(t, "R18", "g_bookshelf2", 22, 7, false, 1, -6); P(t, "R18", "g_bookshelf3", 29, 10, false, 1, -6);
        P(t, "R18", "g_floorCandles", 7); P(t, "R18", "g_gothicWindow", 9.5f, 9, false, 1, -8);
        P(t, "R19", "g_stoneTable", 9, 19); P(t, "R19", "g_bookshelf1", 15, 19, false, 1, -6); P(t, "R19", "g_chain", 9, 8); T("R19", 3, 1);   // tháp pháp sư
        P(t, "R20", "g_statue2", 6); P(t, "R20", "g_gothicWindow", 22, 3, false, 1, -8); P(t, "R20", "g_gothicWindow", 30, 3, false, 1, -8);  // nhà nguyện
        P(t, "R20", "g_statue1", 30); P(t, "R20", "g_floorCandles", 33);
        P(t, "R21", "g_chain", 12, 12); P(t, "R21", "g_webR", 14, 15);                                        // tháp canh
        P(t, "R22", "g_coffinA", 6); P(t, "R22", "g_coffinB", 18); P(t, "R22", "g_coffinA", 30, 0, true); P(t, "R22", "g_coffinB", 42, 0, true);   // hầm mộ
        P(t, "R22", "g_webL", 3, 5.5f);
        P(t, "R23", "g_chain", 8, 5); P(t, "R23", "g_chain", 20, 5); P(t, "R23", "g_chain", 34, 5); P(t, "R23", "g_webR", 50, 5.4f);    // ngục tối
        P(t, "R24", "s_rubbleBig", 12); P(t, "R24", "s_rubble3", 36); P(t, "R24", "s_rubble2", 44);          // hành lang sụp
        P(t, "R25", "g_pillar", 18.5f, 0, false, 1, -7); P(t, "R25", "g_pillar", 31, 0, false, 1, -7);        // phòng ngai (chưa có sprite ngai)
        P(t, "R25", "g_banner1", 22.5f, 8); P(t, "R25", "g_banner2", 27.5f, 8); P(t, "R25", "g_stoneTable", 25, 1);
        P(t, "R25", "g_swords", 15, 4); T("R25", 13); T("R25", 33.5f);

        // ---- Khu D: hang rồng ----
        P(t, "R26", "g_chain", 9, 12); T("R27", 8); T("R27", 30); P(t, "R27", "s_rubble1", 14, 0, false, 1, -5, warm);
        P(t, "R28", "s_rubbleBig", 22, 0, false, 1, -5, warm); P(t, "R28", "s_rubble4", 44, 0, false, 1, -5, warm);
    }
}
