using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// Chuẩn hoá import cho các gói tài nguyên mới (pixel art): Point, không nén, PPU 16, cắt sprite.
public static class F3ImportNewPacks
{

[MenuItem("Tools/Culinaria/F3 Import Props Only")]
    public static void RunProps()
    {
        Setup(R + "tileset.png", SpriteImportMode.Multiple, 16, tex => Windows(tex, 16, GothicProps));
        Setup(R + "MedievalVillageExteriorv1.0/RawAssets/Props_Decor.png", SpriteImportMode.Multiple, 32, tex => Windows(tex, 16, MedievalProps));
        Debug.Log("[IMP] props xong");
    }

    const string R = "Assets/Resouces/";

    [MenuItem("Tools/Culinaria/F3 Import New Packs")]
    public static void Run()
    {
        // Ảnh đơn (đuốc, kim cương, nhân vật, nền)
        foreach (var p in AssetDatabase.FindAssets("t:Texture2D", new[] { R + "Anim", R + "Character/Sprite", R + "Background" }).Select(AssetDatabase.GUIDToAssetPath))
            Setup(p, SpriteImportMode.Single, 16, null);

        // Sheet đạo cụ gothic: cắt tự động theo cụm điểm ảnh
        Setup(R + "tileset.png", SpriteImportMode.Multiple, 16, tex => Windows(tex, 16, GothicProps));
        // Terrain hang + gạch lâu đài: lưới 16px
        Setup(R + "main_lev_buildA.png", SpriteImportMode.Multiple, 16, tex => Grid(tex, 16, 16, "caveA"));
        Setup(R + "main_lev_buildB.png", SpriteImportMode.Multiple, 16, tex => Grid(tex, 16, 16, "castleB"));
        // Đèn tường Medieval: 3x2 khung 48px
        Setup(R + "MedievalVillageExteriorv1.0/RawAssets/Sconce_Spritesheet.png", SpriteImportMode.Multiple, 32, tex => Grid(tex, 48, 48, "sconce"));
        Setup(R + "MedievalVillageExteriorv1.0/RawAssets/Props_Decor.png", SpriteImportMode.Multiple, 32, tex => Windows(tex, 16, MedievalProps));
        AssetDatabase.Refresh();
        Debug.Log("[IMP] xong");
    }

    static void Setup(string path, SpriteImportMode mode, float ppu, System.Func<Texture2D, SpriteMetaData[]> slicer)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[IMP] thiếu " + path); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = mode;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.isReadable = true;
        ti.alphaIsTransparency = true;
        ti.spritePixelsPerUnit = ppu;
        ti.SaveAndReimport();
        if (slicer != null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var metas = slicer(tex);
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();
            var rects = metas.Select(m => new SpriteRect { name = m.name, rect = m.rect, alignment = (SpriteAlignment)m.alignment, pivot = m.pivot, spriteID = GUID.Generate() }).ToArray();
            dp.SetSpriteRects(rects);
            var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names != null) names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            dp.Apply();
            ti.SaveAndReimport();
        }
        var n = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count();
        Debug.Log($"[IMP] {path} → {n} sprite, ppu {ppu}");
    }

    static SpriteMetaData[] Grid(Texture2D tex, int w, int h, string prefix)
    {
        var list = new List<SpriteMetaData>();
        var px = tex.GetPixels32();
        int cols = tex.width / w, rows = tex.height / h;
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
        {
            int x0 = c * w, y0 = tex.height - (r + 1) * h;
            bool any = false;
            for (int y = y0; y < y0 + h && !any; y++) for (int x = x0; x < x0 + w; x++) if (px[y * tex.width + x].a > 8) { any = true; break; }
            if (!any) continue;
            list.Add(new SpriteMetaData { name = $"{prefix}_{c}_{r}", rect = new Rect(x0, y0, w, h), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
        }
        return list.ToArray();
    }

    static SpriteMetaData[] Auto(Texture2D tex, int minSize)
    {
        var rects = InternalSpriteUtility.GenerateAutomaticSpriteRectangles(tex, minSize, 0);
        var list = new List<SpriteMetaData>();
        // sắp theo hàng (trên→dưới), cột (trái→phải) để đánh số ổn định
        int i = 0;
        foreach (var rc in rects.OrderByDescending(r => Mathf.Round(r.yMax / 8f)).ThenBy(r => r.x))
            list.Add(new SpriteMetaData { name = $"{tex.name}_{i++}", rect = rc, alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(0.5f, 0f) });
        return list.ToArray();
    }


// (tên, cột0, hàng0, cột1, hàng1) theo ô 16px, hàng 0 ở TRÊN. Rect được cắt khít theo điểm ảnh trong vùng.
    static readonly (string n, float c0, float r0, float c1, float r1)[] GothicProps =
    {
        ("g_barrelSide", 0, 1, 1.6f, 2), ("g_crate", 0, 2, 1, 3), ("g_barrel", 2, 1.85f, 3, 3),
        ("g_bench", 3, 1, 4.98f, 3), ("g_stoneTable", 5, 1.9f, 7, 3),
        ("g_halberds", 7.9f, 1, 10, 3), ("g_candelabra", 7.9f, 3, 10, 4),
        ("g_banner1", 3, 3, 3.97f, 5), ("g_banner2", 3, 5, 3.97f, 7),
        ("g_shield", 4.05f, 5, 4.95f, 6), ("g_swords", 5, 5, 6, 6), ("g_painting", 2, 5, 3, 6),
        ("g_pillar", 0, 6, 2, 10),
        ("g_torch0", 11.95f, 5, 12.92f, 7), ("g_torch1", 12.92f, 5, 13.92f, 7), ("g_torch2", 13.92f, 5, 14.92f, 7), ("g_torch3", 14.92f, 5, 15.9f, 7),
        ("g_chain", 18.1f, 5, 19, 7),
        ("g_statue1", 11, 7, 13, 10), ("g_statue2", 13, 7, 14.95f, 10),
        ("g_coffinA", 14.98f, 7, 16.92f, 10), ("g_coffinB", 16.95f, 7, 19, 10),
        ("g_webL", 12.95f, 1, 15, 4), ("g_webR", 16, 1, 19, 4.2f),
        ("g_doorSmall", 19, 0, 21.05f, 3),
        ("g_fireplaceOff", 19, 3, 21, 5), ("g_fireplace0", 21, 3, 23, 5), ("g_fireplace1", 23, 3, 25, 5),
        ("g_curtainBrown", 25, 0, 27, 3), ("g_curtainPurple", 27, 0, 29, 3), ("g_curtainRed", 29, 0, 31, 3),
        ("g_archWindow", 26, 3, 28, 6), ("g_gothicWindow", 28.4f, 3, 31, 7),
        ("g_floorCandles", 23, 5, 25.95f, 6.2f),
        ("g_bookshelf0", 23, 7, 25, 10), ("g_bookshelf1", 25, 7, 27, 10), ("g_bookshelf2", 27, 7, 29, 10), ("g_bookshelf3", 29, 7, 31, 10),
    };

    static readonly (string n, float c0, float r0, float c1, float r1)[] MedievalProps =
    {
        ("m_barrel", 0, 0, 2.9f, 3), ("m_crate", 3, 0, 6.05f, 3.05f), ("m_bunting", 6.08f, 0, 12, 2.5f),
        ("m_cart", 3.5f, 3.1f, 11.5f, 6.2f), ("m_crateStack", 3, 6.6f, 9.04f, 12),
    };

    static SpriteMetaData[] Windows(Texture2D tex, int cell, (string n, float c0, float r0, float c1, float r1)[] defs)
    {
        var px = tex.GetPixels32();
        var list = new List<SpriteMetaData>();
        foreach (var d in defs)
        {
            int x0 = Mathf.RoundToInt(d.c0 * cell), x1 = Mathf.Min(tex.width, Mathf.RoundToInt(d.c1 * cell));
            int top0 = Mathf.RoundToInt(d.r0 * cell), top1 = Mathf.Min(tex.height, Mathf.RoundToInt(d.r1 * cell));
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int t = top0; t < top1; t++)
            {
                int y = tex.height - 1 - t;   // texture y tính từ dưới
                for (int x = x0; x < x1; x++)
                    if (px[y * tex.width + x].a > 3) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            }
            if (maxX < 0) { Debug.LogWarning("[IMP] vùng rỗng: " + d.n); continue; }
            list.Add(new SpriteMetaData { name = d.n, rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
        }
        return list.ToArray();
    }
}
