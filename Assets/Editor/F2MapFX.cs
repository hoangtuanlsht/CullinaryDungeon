using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Tạo prefab PixelFX (Assets/Prefab/pixelFX) và đặt hiệu ứng môi trường cho SecondFloorScene_v2.
public static class F2MapFX
{
    const string Sheets = "Assets/Resouces/pixelfx 1/pixelfx-main/sheets/";
    const string CleanDir = "Assets/Resouces/pixelfx/F2Clean/";   // bản ảnh đã chỉnh cho hợp tầng 2
    const string AnimDir = "Assets/Animation/PixelFX/";
    const string PrefabDir = "Assets/Prefab/pixelFX/";
    const string ScenePath = "Assets/Scenes/SecondFloorScene_v2.unity";
    const float PPU = 100f;          // giống waterfall.png
    const float PixelScale = 6.25f;  // 1 pixel của pack = 1 pixel tile 16px
    const float D = 18.7f;

    // tên, nhóm, hệ số kích thước, loop, thứ tự vẽ (+ trước nhân vật), bắt đầu ngẫu nhiên
    static readonly (string name, string group, float size, bool loop, int order, bool rand)[] Fx =
    {
        ("fireflies", "weather", 1f, true, 5, true),
        ("falling-leaves", "weather", 1f, true, 5, true),
        ("god-rays", "weather", 1f, true, 0, true),
        ("butterfly", "critters", 0.5f, true, 5, true),
        ("bird", "critters", 0.6f, true, 0, true),
        ("bubbles", "water", 0.6f, true, 1, true),
        ("drip", "water", 1f, true, 1, true),
        ("loot-beam", "pickups", 0.6f, true, 1, false),
        ("key-glint", "pickups", 0.5f, true, 2, false),
        ("respawn-beam", "spawn", 0.7f, false, 1, false),
        ("spawn-portal", "spawn", 0.6f, false, 1, false),
    };

    // khung ảnh thời tiết: 320x180 px -> 20 x 11.25 đơn vị ở tỉ lệ prefab; phần nhìn thấy theo chiều dọc
    static readonly Dictionary<string, float> WeatherCut = new Dictionary<string, float>
        { { "fireflies", 0.80f }, { "falling-leaves", 0.86f }, { "god-rays", 0.86f } };

    [Serializable] class JRect { public int x, y, w, h; }
    [Serializable] class JFrame { public string filename; public JRect frame; public int duration; }
    [Serializable] class JPfx { public int fps; public bool loop; public int[] pivot; public int[] frameSize; }
    [Serializable] class JMeta { public JPfx pixelfx; }
    [Serializable] class JRoot { public JFrame[] frames; public JMeta meta; }

    [MenuItem("Tools/Culinaria/F2 Map FX - tao lai prefab hieu ung lap")]
    static void RebuildLooping()
    {
        foreach (var f in Fx.Where(f => f.rand)) AssetDatabase.DeleteAsset(PrefabDir + f.name + ".prefab");
        Run();
    }

    [MenuItem("Tools/Culinaria/F2 Map FX (prefab + đặt vào scene)")]
    static void Run()
    {
        var made = new List<string>();
        foreach (var f in Fx)
        {
            string p = PrefabDir + f.name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(p) == null) { if (BuildPrefab(f)) made.Add(f.name); }
        }
        Debug.Log("[F2FX] Prefab mới: " + (made.Count > 0 ? string.Join(", ", made) : "không có (đã có sẵn)"));
        Place();
    }

    static Type FindType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

    // ---------- prefab ----------
    static bool BuildPrefab((string name, string group, float size, bool loop, int order, bool rand) f)
    {
        string png = Sheets + f.group + "/" + f.name + ".png";
        if (AssetImporter.GetAtPath(CleanDir + f.name + ".png") != null) png = CleanDir + f.name + ".png";
        string jsonPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), Sheets + f.group + "/" + f.name + ".json");
        if (!File.Exists(jsonPath)) { Debug.LogError("[F2FX] thiếu " + jsonPath); return false; }
        var j = JsonUtility.FromJson<JRoot>(File.ReadAllText(jsonPath));
        var imp = AssetImporter.GetAtPath(png) as TextureImporter;
        if (imp == null || j?.frames == null || j.frames.Length == 0) { Debug.LogError("[F2FX] lỗi đọc " + png); return false; }

        int texH = 0;
        foreach (var fr in j.frames) texH = Mathf.Max(texH, fr.frame.y + fr.frame.h);
        int fw = j.frames[0].frame.w, fh = j.frames[0].frame.h;
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        if (j.meta?.pixelfx?.pivot != null && j.meta.pixelfx.pivot.Length == 2)
            pivot = new Vector2(j.meta.pixelfx.pivot[0] / (float)fw, 1f - j.meta.pixelfx.pivot[1] / (float)fh);

        var metas = new SpriteMetaData[j.frames.Length];
        for (int i = 0; i < j.frames.Length; i++)
        {
            var r = j.frames[i].frame;
            metas[i] = new SpriteMetaData
            {
                name = f.name + "_" + i,
                rect = new Rect(r.x, texH - r.y - r.h, r.w, r.h),
                alignment = (int)SpriteAlignment.Custom,
                pivot = pivot
            };
        }
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.spritePixelsPerUnit = PPU;
        imp.maxTextureSize = 8192;
#pragma warning disable 618
        imp.spritesheet = metas;
#pragma warning restore 618
        imp.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>()
            .Select(s => (s, idx: int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out var k) ? k : -1))
            .Where(t => t.idx >= 0).OrderBy(t => t.idx).Select(t => t.s).ToArray();
        if (sprites.Length == 0) { Debug.LogError("[F2FX] không cắt được sprite " + png); return false; }

        Directory.CreateDirectory(AnimDir); Directory.CreateDirectory(PrefabDir);
        int fps = j.meta?.pixelfx != null && j.meta.pixelfx.fps > 0 ? j.meta.pixelfx.fps : 30;
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / (float)fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / (float)fps, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var st = AnimationUtility.GetAnimationClipSettings(clip); st.loopTime = f.loop; AnimationUtility.SetAnimationClipSettings(clip, st);
        string clipPath = AnimDir + f.name + ".anim";
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        string ctrlPath = AnimDir + f.name + ".controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);

        var go = new GameObject(f.name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprites[0];
        var wf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "waterfall_0.prefab");
        var wsr = wf != null ? wf.GetComponentInChildren<SpriteRenderer>() : null;
        if (wsr != null) { sr.sortingLayerID = wsr.sortingLayerID; sr.sortingOrder = wsr.sortingOrder + f.order; sr.sharedMaterial = wsr.sharedMaterial; }
        else sr.sortingOrder = f.order;
        var an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        an.cullingMode = AnimatorCullingMode.CullCompletely;
        if (f.rand) { var t = FindType("AnimRandomStart"); if (t != null) go.AddComponent(t); }
        float s = PixelScale * f.size;
        go.transform.localScale = new Vector3(s, s, 1);
        PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + f.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return true;
    }

    // ---------- đặt vào scene ----------
    static Tilemap ground, swamp;
    static readonly Dictionary<string, RectInt[]> Rooms = new Dictionary<string, RectInt[]>
    {
        { "F2_R01", new[] { new RectInt(0, -100, 144, 20) } },
        { "F2_R04", new[] { new RectInt(108, -80, 24, 40) } },
        { "F2_R05", new[] { new RectInt(144, -110, 54, 40) } },
        { "F2_R06", new[] { new RectInt(162, -70, 36, 20) } },
        { "F2_R07", new[] { new RectInt(198, -100, 54, 20) } },
        { "F2_R08", new[] { new RectInt(216, -80, 24, 40) } },
        { "F2_R09", new[] { new RectInt(54, -120, 90, 20) } },
        { "F2_R10", new[] { new RectInt(252, -100, 36, 20) } },
        { "F2_R11", new[] { new RectInt(288, -110, 54, 40) } },
        { "F2_R13", new[] { new RectInt(144, -130, 54, 20), new RectInt(198, -130, 54, 30) } },
        { "F2_R14", new[] { new RectInt(36, -80, 72, 20) } },
        { "F2_R15", new[] { new RectInt(240, -80, 48, 20) } },
        { "F2_R16", new[] { new RectInt(342, -110, 24, 40) } },
    };

    static void Place()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var roots = scene.GetRootGameObjects();
        foreach (var tm in roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)))
        {
            if (tm.name == "F2_Ground") ground = tm;
            if (tm.name == "F2_Swamp") swamp = tm;
        }
        var roomObjs = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.StartsWith("F2_R") && t.GetComponent<Collider2D>() != null && t.GetComponents<MonoBehaviour>().Any(m => m != null && m.GetType().Name == "Room"))
            .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
        if (ground == null) { Debug.LogError("[F2FX] không thấy F2_Ground"); return; }

        var log = new List<string>();
        foreach (var kv in Rooms)
        {
            if (!roomObjs.TryGetValue(kv.Key, out var room)) { log.Add(kv.Key + ": không thấy phòng"); continue; }
            var old = room.Find("MapFX");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var fx = new GameObject("MapFX");
            Undo.RegisterCreatedObjectUndo(fx, "MapFX");
            SceneManager.MoveGameObjectToScene(fx, scene);
            fx.transform.SetParent(room, false);
            fx.transform.position = Vector3.zero; fx.transform.rotation = Quaternion.identity;
            var ls = room.lossyScale; fx.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f);
            int n = Spec(kv.Key, kv.Value, fx.transform);
            log.Add(kv.Key + ":" + n);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[F2FX] Đặt hiệu ứng: " + string.Join(" | ", log));
    }

    static int Spec(string id, RectInt[] rs, Transform p)
    {
        int c = 0;
        void Air(string fx, int k) { c += PutAir(fx, rs, k, p); }
        void W(string fx) { c += PutWeather(fx, rs, p); }
        switch (id)
        {
            case "F2_R01": W("fireflies"); c += PutLeaves(rs, p); W("god-rays"); Air("butterfly", 1); c += PutSwamp("bubbles", rs, 2, p); break;
            case "F2_R04": c += PutMarker("loot-beam", new Vector2(125.5f, -49.5f), p, true); break;
            case "F2_R05": W("fireflies"); c += PutLeaves(rs, p); Air("butterfly", 1); Air("bird", 1); break;
            case "F2_R06": c += PutMarker("respawn-beam", new Vector2(184.5f, -67.5f), p, false); break;
            case "F2_R07": c += PutSwamp("bubbles", rs, 3, p); W("fireflies"); c += PutDrip(rs, 1, p); break;
            case "F2_R08": c += PutMarker("loot-beam", new Vector2(221.5f, -49.5f), p, true); break;
            case "F2_R09": W("fireflies"); c += PutDrip(rs, 2, p); break;
            case "F2_R10": c += PutMarker("respawn-beam", new Vector2(257.5f, -95.5f), p, false); break;
            case "F2_R11": c += PutLeaves(rs, p); W("god-rays"); break;
            case "F2_R13": c += PutSwamp("bubbles", rs, 2, p); W("fireflies"); c += PutDrip(rs, 1, p); break;
            case "F2_R14": Air("butterfly", 1); Air("bird", 1); W("god-rays"); c += PutMarker("key-glint", new Vector2(85.5f, -66.5f), p, true); break;
            case "F2_R15": W("fireflies"); W("god-rays"); c += PutMarker("key-glint", new Vector2(281.5f, -72.5f), p, true); break;
            case "F2_R16": c += PutMarker("spawn-portal", new Vector2(359.5f, -106.5f), p, false); W("fireflies"); break;
        }
        return c;
    }

    static bool Solid(int x, int y) => ground.HasTile(new Vector3Int(x, y, 0));
    static bool Wet(int x, int y) => swamp != null && swamp.HasTile(new Vector3Int(x, y, 0));

    static GameObject Spawn(string fx, Transform p, Vector3 pos, float extraScale = 1f, bool active = true, string suffix = "")
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + fx + ".prefab");
        if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, p.gameObject.scene);
        go.transform.SetParent(p, true);
        go.transform.position = pos;
        go.transform.localScale = pf.transform.localScale * extraScale;
        go.name = fx + suffix;
        go.SetActive(active);
        return go;
    }

    // khoảng không khí lớn nhất trong cột x của hình chữ nhật r: (đáy, đỉnh) tính theo ô
    static bool AirRun(int x, RectInt r, out int lo, out int hi)
    {
        lo = 0; hi = -1; int start = int.MinValue;
        for (int y = r.yMin; y <= r.yMax; y++)
        {
            bool air = y < r.yMax && !Solid(x, y) && !Wet(x, y);
            if (air && start == int.MinValue) start = y;
            if (!air && start != int.MinValue) { if (y - start > hi - lo + 1) { lo = start; hi = y - 1; } start = int.MinValue; }
        }
        return hi >= lo;
    }

    // hiệu ứng thời tiết: phủ kín bề ngang phòng, đỉnh khung sát trần, phần nhìn thấy kết thúc ở sàn
    static int PutWeather(string fx, RectInt[] rs, Transform p)
    {
        float cut = WeatherCut[fx];
        float z = fx == "god-rays" ? 4f : 0f;
        float depth = (D + z) / D;
        // lá rơi giữ đúng cỡ pixel của pack (không phóng to), phủ chiều cao bằng nhiều hàng
        bool fixedSize = fx == "falling-leaves";
        int placed = 0;
        foreach (var r in rs)
        {
            int h0 = AirRun(r.xMin + r.width / 2, r, out var l0, out var u0) ? u0 - l0 + 1 : r.height - 4;
            float e0 = fixedSize ? 1f : Mathf.Clamp(h0 / (cut * 11.25f), 0.8f, 2.2f);
            float tileW = fixedSize ? 26f : 20f * e0 * 0.8f;   // lá: để hở giữa các khung cho thưa; khác: chồng mép 20%
            int n = Mathf.Max(1, Mathf.CeilToInt((r.width - 2) / tileW));
            for (int i = 0; i < n; i++)
            {
                float cx0 = r.xMin + 1 + (i + 0.5f) * (r.width - 2) / n;
                if (!AirRun(Mathf.FloorToInt(cx0), r, out var lo, out var hi)) continue;
                int h = hi - lo + 1;
                if (h < 4) continue;
                float e = fixedSize ? 1f : Mathf.Clamp(h / (cut * 11.25f), 0.8f, 2.2f);
                float H = 11.25f * e;
                float top0 = hi + 1 + (fixedSize ? 0.01f : 0.06f) * H;   // mép mờ phía trên nằm sát trần
                int rows = fixedSize ? Mathf.Max(1, Mathf.CeilToInt((h + 0.06f * H) / (cut * H * 0.85f))) : 1;
                float step = rows > 1 ? (h + 0.06f * H - cut * H) / (rows - 1) : 0f;
                for (int k = 0; k < rows; k++)
                {
                    float cx = cx0 + ((k % 2 == 1) ? tileW * 0.5f : 0f);
                    if (cx > r.xMax - 1) cx -= tileW;
                    float top = top0 - k * step;
                    var pos = new Vector3(cx, top - (170f / 180f) * H, z);
                    var inst = Spawn(fx, p, pos, e * depth, true, "_" + placed);
                    if (inst != null) { placed++; if (fixedSize) inst.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.85f); }
                }
            }
        }
        return placed;
    }

    // ---------- lá rơi bằng Particle System (lá nhỏ, không bị khung ảnh giới hạn) ----------
    const string LeafTex = CleanDir + "leaves_atlas.png";
    const string LeafPrefab = PrefabDir + "falling-leaves-particles.prefab";

    static GameObject LeavesPrefab()
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(LeafPrefab);
        if (pf != null) return pf;
        var imp = AssetImporter.GetAtPath(LeafTex) as TextureImporter;
        if (imp == null) { Debug.LogError("[F2FX] thiếu " + LeafTex); return null; }
        imp.textureType = TextureImporterType.Default;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(LeafTex);
        string matPath = AnimDir + "F2Leaves.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.mainTexture = tex;
        EditorUtility.SetDirty(mat);

        var go = new GameObject("falling-leaves-particles");
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);   // ô 16px; lá thật ~0.2–0.35 đơn vị
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 1f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var em = ps.emission; em.rateOverTime = 1f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(10f, 0.5f, 0.1f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.5f);
        vel.y = new ParticleSystem.MinMaxCurve(-1.3f, -0.7f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.35f; noise.scrollSpeed = 0.2f;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var tsa = ps.textureSheetAnimation; tsa.enabled = true;
        tsa.mode = ParticleSystemAnimationMode.Grid; tsa.numTilesX = 4; tsa.numTilesY = 2;
        tsa.animation = ParticleSystemAnimationType.WholeSheet;
        tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);   // mỗi lá một hình ngẫu nhiên, không đổi
        var coll = ps.collision; coll.enabled = true; coll.type = ParticleSystemCollisionType.World;
        coll.mode = ParticleSystemCollisionMode.Collision2D; coll.lifetimeLoss = 1f; coll.collidesWith = 1 << 6; coll.radiusScale = 0.3f;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mat; rend.renderMode = ParticleSystemRenderMode.Billboard;
        var wf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "waterfall_0.prefab");
        var wsr = wf != null ? wf.GetComponentInChildren<SpriteRenderer>() : null;
        if (wsr != null) { rend.sortingLayerID = wsr.sortingLayerID; rend.sortingOrder = wsr.sortingOrder + 5; }
        PrefabUtility.SaveAsPrefabAsset(go, LeafPrefab);
        UnityEngine.Object.DestroyImmediate(go);
        return AssetDatabase.LoadAssetAtPath<GameObject>(LeafPrefab);
    }

    // một hệ hạt cho mỗi hình chữ nhật phòng: sinh dọc trần, lá chạm đất thì biến mất
    static int PutLeaves(RectInt[] rs, Transform p)
    {
        var pf = LeavesPrefab();
        if (pf == null) return 0;
        int placed = 0;
        foreach (var r in rs)
        {
            var tops = new List<int>(); var hs = new List<int>();
            for (int x = r.xMin + 2; x < r.xMax - 2; x += 3)
                if (AirRun(x, r, out var lo, out var hi)) { tops.Add(hi + 1); hs.Add(hi - lo + 1); }
            if (tops.Count == 0) continue;
            tops.Sort(); hs.Sort();
            float top = tops[tops.Count / 2], h = hs[hs.Count / 2];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, p.gameObject.scene);
            go.transform.SetParent(p, true);
            go.transform.position = new Vector3(r.center.x, top - 0.6f, 0f);
            go.transform.localScale = Vector3.one;
            go.name = "falling-leaves-particles_" + placed;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(h / 1.3f + 1f, h / 0.7f + 1f);
            var em = ps.emission; em.rateOverTime = Mathf.Max(0.6f, r.width * 0.045f);
            var sh = ps.shape; sh.scale = new Vector3(r.width - 2f, 0.5f, 0.1f);
            placed++;
        }
        return placed;
    }

    // bướm, chim: rải theo bề ngang phòng
    static int PutAir(string fx, RectInt[] rs, int k, Transform p)
    {
        int xmin = rs.Min(r => r.xMin), xmax = rs.Max(r => r.xMax);
        int placed = 0;
        for (int i = 0; i < k; i++)
        {
            float t = (i + 0.5f) / k;
            t = Mathf.Clamp01(t + (fx == "bird" ? 0.17f : -0.13f));
            int x = Mathf.RoundToInt(Mathf.Lerp(xmin + 4, xmax - 4, t));
            var r = rs.FirstOrDefault(rr => x >= rr.xMin && x < rr.xMax);
            if (r.width == 0) r = rs[0];
            if (!AirRun(x, r, out var lo, out var hi)) continue;
            Vector3 pos; float extra = 1f;
            if (fx == "bird") { pos = new Vector3(x + 0.5f, Mathf.Lerp(lo, hi, 0.7f), 8f); extra = (D + 8f) / D; }
            else pos = new Vector3(x + 0.5f, Mathf.Lerp(lo, hi + 1, 0.45f), 0);
            if (Spawn(fx, p, pos, extra, true, "_" + i) != null) placed++;
        }
        return placed;
    }

    // bong bóng trên các vũng đầm lầy lớn nhất
    static int PutSwamp(string fx, RectInt[] rs, int k, Transform p)
    {
        if (swamp == null) return 0;
        var runs = new List<(int x0, int x1, int y)>();
        foreach (var r in rs)
            for (int y = r.yMin; y < r.yMax; y++)
            {
                int start = int.MinValue;
                for (int x = r.xMin; x <= r.xMax; x++)
                {
                    bool top = x < r.xMax && Wet(x, y) && !Wet(x, y + 1) && !Solid(x, y + 1);
                    if (top && start == int.MinValue) start = x;
                    if (!top && start != int.MinValue) { runs.Add((start, x - 1, y)); start = int.MinValue; }
                }
            }
        int placed = 0;
        foreach (var run in runs.OrderByDescending(q => q.x1 - q.x0).Take(k))
        {
            float w = run.x1 - run.x0 + 1;
            float extra = Mathf.Clamp(w / 4f, 0.5f, 1.2f);
            if (Spawn(fx, p, new Vector3((run.x0 + run.x1 + 1) / 2f, run.y + 1, 0), extra, true, "_" + placed) != null) placed++;
        }
        return placed;
    }

    // giọt nước: rơi từ trần xuống sàn (khoảng trống 3–8 ô)
    static int PutDrip(RectInt[] rs, int k, Transform p)
    {
        var cands = new List<(int x, int floor, int gap)>();
        foreach (var r in rs)
            for (int x = r.xMin + 3; x < r.xMax - 3; x++)
                for (int y = r.yMax - 1; y > r.yMin; y--)
                {
                    if (!Solid(x, y) || Solid(x, y - 1)) continue;
                    int yy = y - 1;
                    while (yy > r.yMin - 1 && !Solid(x, yy) && !Wet(x, yy)) yy--;
                    int gap = y - 1 - yy;
                    if (gap >= 3 && gap <= 8 && Solid(x, yy)) cands.Add((x, yy + 1, gap));
                    break;
                }
        if (cands.Count == 0) return 0;
        cands = cands.OrderBy(c => c.x).ToList();
        int placed = 0;
        for (int i = 0; i < k; i++)
        {
            var c = cands[Mathf.Clamp(Mathf.RoundToInt((i + 0.5f) / k * cands.Count - 0.5f), 0, cands.Count - 1)];
            // khung 88px = 5.5 đơn vị; giọt bắt đầu ở ~15% khung, chân ở pivot (78/88)
            float H = c.gap / (78f / 88f - 0.15f);
            float extra = Mathf.Clamp(H / 5.5f, 0.6f, 2f);
            if (Spawn("drip", p, new Vector3(c.x + 0.5f, c.floor, 0), extra, true, "_" + i) != null) placed++;
        }
        return placed;
    }

    // đặt ở sàn ngay dưới một điểm mốc
    static int PutMarker(string fx, Vector2 m, Transform p, bool active)
    {
        int x = Mathf.FloorToInt(m.x), y = Mathf.FloorToInt(m.y);
        int yy = y; while (yy > y - 12 && !Solid(x, yy)) yy--;
        float floor = Solid(x, yy) ? yy + 1 : m.y;
        return Spawn(fx, p, new Vector3(m.x, floor, 0), 1f, active) != null ? 1 : 0;
    }
}
