using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// [Tầng 6] Hiệu ứng bản đồ PixelFX (skill culinaria-map-fx). Chạy ở cuối mỗi lần dựng tầng 6 (sau khi gộp phòng).
///  - Prefab mới (chỉ tạo nếu chưa có, ở Assets/Prefab/pixelFX): flame-pillar, shadow-flames, spirit-flame, rune-circle, flying-skull.
///    Mỗi prefab: SpriteRenderer + Animator (Cull Completely) + AnimRandomStart (lệch nhịp) + Light2D (trừ cột lửa). Không Collider.
///  - Prefab có sẵn dùng lại: respawn-beam (điểm lưu), key-glint (rương), loot-beam (rương bí mật R11).
///  - Đặt vào con "MapFX" của phòng camera (phòng dẫn đầu sau khi gộp). Mỗi chỗ đặt đều kiểm tra có sàn đặc bên dưới, không thì bỏ và ghi log.
///  - Không đụng RoomGlow, collider phòng, EnemyWall, vcam.
public static partial class SixthFloorBuilder
{
    // Sprite PixelFX ở PPU 100 nhỏ so với ô 1 đơn vị của tầng 6: phóng to để lửa cao ~2 ô như đuốc, cột lửa ~5 ô
    const float SPillar = 2.5f, SShadow = 3.5f, SSpirit = 3f, SRune = 3f, SSkull = 2.6f;
    const string FxSheetDir = "Assets/Resouces/pixelfx 1/pixelfx-main/sheets/";
    const string FxAnimDir = "Assets/Animation/PixelFX/";
    const string FxPrefabDir = "Assets/Prefab/pixelFX/";
    static readonly Color FxPurple = new Color(0.72f, 0.42f, 1f);
    static readonly Color FxTeal = new Color(0.42f, 0.95f, 1f);
    static readonly Color FxWarm = new Color(1f, 0.70f, 0.40f);

    static void BuildMapFX(UnityEngine.SceneManagement.Scene scene)
    {
        var fxLog = new List<string>();
        var pillar = EnsureFxPrefab("fire", "flame-pillar", 2, null, 0, 0, fxLog);                 // tắt dần thành khói → không gắn đèn
        var shadow = EnsureFxPrefab("shadow", "shadow-flames", -3, FxPurple, 5f, 1.2f, fxLog);
        var spirit = EnsureFxPrefab("fire", "spirit-flame", -3, FxTeal, 4f, 1.0f, fxLog);
        var rune = EnsureFxPrefab("arcane", "rune-circle", -3, FxPurple, 4f, 0.8f, fxLog);
        var skull = EnsureFxPrefab("critters", "flying-skull", -6, FxWarm, 3f, 0.8f, fxLog);
        var beam = AssetDatabase.LoadAssetAtPath<GameObject>(FxPrefabDir + "respawn-beam.prefab");
        var glint = AssetDatabase.LoadAssetAtPath<GameObject>(FxPrefabDir + "key-glint.prefab");
        var loot = AssetDatabase.LoadAssetAtPath<GameObject>(FxPrefabDir + "loot-beam.prefab");

        var roomRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "______Room_F6_____");
        if (roomRoot == null) { log.Add("MapFX: không thấy ______Room_F6_____ → bỏ qua."); return; }
        var leaderOf = rooms.Keys.ToDictionary(k => k, k => k);
        foreach (var (leader, members) in F6RoomMerge.Groups)
            foreach (var m in members) leaderOf[m.Substring(3)] = leader.Substring(3);

        string FrameAt(float x, float y) => rooms.FirstOrDefault(kv => kv.Value.r.Contains(new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y)))).Key;

        int n = 0;
        // Đặt một hiệu ứng tại toạ độ thế giới; needGround: phải có ô đặc ngay dưới chân và ô đứng còn trống.
        void PutAt(GameObject prefab, string label, float x, float y, float scale, bool needGround, bool active = true)
        {
            if (prefab == null) { fxLog.Add("thiếu prefab cho " + label); return; }
            string frame = FrameAt(x, y);
            if (frame == null) { fxLog.Add($"{label} @({x},{y}) ngoài mọi phòng"); return; }
            int ix = Mathf.FloorToInt(x), iy = Mathf.RoundToInt(y);
            if (needGround && (!Open(ix, iy) || !Get(solid, ix, iy - 1))) { fxLog.Add($"bỏ {label} ({frame}): không có sàn ở x={ix}, y={iy}"); return; }
            var roomT = roomRoot.transform.Find("F6_" + leaderOf[frame]);
            if (roomT == null) { fxLog.Add($"{label}: không thấy phòng F6_{leaderOf[frame]}"); return; }
            var fx = roomT.Find("MapFX");
            if (fx == null) { fx = new GameObject("MapFX").transform; fx.SetParent(roomT, false); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = $"{prefab.name} ({label})";
            go.transform.SetParent(fx, true);
            go.transform.position = new Vector3(x, y, 0);
            go.transform.localScale = prefab.transform.localScale * scale;
            foreach (var lt in go.GetComponentsInChildren<Light2D>(true))          // bù tỉ lệ cha để bán kính đèn tính bằng ô thật
                lt.transform.localScale = Vector3.one / Mathf.Max(0.01f, go.transform.lossyScale.x);
            go.SetActive(active);
            n++;
        }
        void Floor(GameObject p, string label, string room, float dx, float scale, float standDy = 0)
            => PutAt(p, label, rooms[room].r.xMin + dx, rooms[room].Floor + standDy, scale, true);
        void Air(GameObject p, string label, string room, float dx, float dy, float scale)
            => PutAt(p, label, rooms[room].r.xMin + dx, rooms[room].Floor + dy, scale, false);
        // Cột lửa phun từ mặt dung nham (mặt dung nham thấp hơn mặt đất 1 ô = r.yMin). off, w giống lệnh Lava() trong BuildGeometry.
        void Erupt(string room, int off, int w)
        {
            var r = rooms[room]; float x = r.r.xMin + off + w / 2f;
            if (!Get(lava, Mathf.FloorToInt(x), r.r.yMin - 1)) { fxLog.Add($"bỏ cột lửa {room}+{off}: không phải dung nham"); return; }
            PutAt(pillar, $"phun từ hố dung nham {room}", x, r.r.yMin, SPillar, false);
        }

        // ---- Khu A, B: cột lửa ở các hố lớn KHÔNG bắt buộc nhảy chính xác (tránh hố Dash và cầu gãy để khỏi rối mắt) ----
        Erupt("R03", 13, 7);
        Erupt("R04", 16, 5); Erupt("R04", 32, 5);
        Erupt("R08", 14, 4); Erupt("R08", 34, 4);
        Erupt("R10", 36, 5);
        Air(skull, "hang hắc diện thạch", "R07", 30, 5, SSkull);
        Air(skull, "lò đá", "R09", 6, 7, SSkull);

        // ---- Khu C: lâu đài pháp sư ----
        Air(skull, "hành lang tranh", "R16", 31, 4, SSkull);
        int ledge = CellBottom(5) + 1;                                                   // tháp pháp sư R19: sàn phòng thí nghiệm = ledge + 9
        var r19 = rooms["R19"].r;
        PutAt(rune, "dưới chỗ nhặt Dash", r19.xMin + 12.5f, ledge + 9, SRune, true);
        PutAt(shadow, "phòng thí nghiệm", r19.xMin + 7.5f, ledge + 9, SShadow, true);
        Floor(shadow, "nhà nguyện", "R20", 22f, SShadow);
        Floor(shadow, "nhà nguyện", "R20", 28.5f, SShadow);
        Floor(spirit, "hồn trong hầm mộ", "R22", 9.5f, SSpirit);
        Floor(spirit, "hồn trong hầm mộ", "R22", 24.5f, SSpirit);
        Floor(spirit, "hồn trong hầm mộ", "R22", 45.5f, SSpirit);
        Air(skull, "hầm mộ", "R22", 27, 5, SSkull);
        Floor(spirit, "hồn tù nhân", "R23", 17.5f, SSpirit);
        Floor(spirit, "hồn tù nhân", "R23", 30.5f, SSpirit);
        Floor(shadow, "bệ ngai", "R25", 21f, SShadow, 1);
        Floor(shadow, "bệ ngai", "R25", 28.5f, SShadow, 1);
        Floor(rune, "trước bệ ngai", "R25", 25f, SRune, 1);

        // ---- Khu D: đường tới hang rồng ----
        Air(skull, "giếng xuống hang rồng", "R26", 9, 10, SSkull);
        Air(skull, "tiền phòng rồng", "R27", 14, 5, SSkull);

        // ---- Cơ chế bản đồ: điểm lưu, rương, rương bí mật ----
        var interact = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "______Interact_F6_____");
        if (interact != null)
            foreach (var cp in interact.GetComponentsInChildren<CheckPoint>(true))
            {
                PutAt(beam, cp.name, cp.transform.position.x, cp.transform.position.y, 1f, false);
                if (cp.name.Contains("SuoiMana")) PutAt(rune, "Suối Mana", cp.transform.position.x, cp.transform.position.y - 1.5f, SRune, true);
            }
        foreach (var c in chests)
        {
            PutAt(glint, c.id, c.x, c.y + 1.5f, 1f, false);
            if (c.id == "F6_Chest02") PutAt(loot, "rương bí mật R11", c.x, c.y, 1f, false);
        }

        log.Add($"MapFX: đặt {n} hiệu ứng" + (fxLog.Count > 0 ? " | " + string.Join(" | ", fxLog) : "") + ".");
    }

    /// Tạo prefab hiệu ứng (nếu chưa có): cắt sheet theo json của PixelFX, .anim lặp, .controller, prefab. Có rồi thì dùng lại, không ghi đè.
    static GameObject EnsureFxPrefab(string group, string name, int order, Color? lightColor, float radius, float intensity, List<string> fxLog)
    {
        string prefabPath = FxPrefabDir + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        string png = FxSheetDir + group + "/" + name + ".png";
        string jsonPath = FxSheetDir + group + "/" + name + ".json";
        if (!File.Exists(jsonPath)) { fxLog.Add("không thấy " + jsonPath); return null; }
        string json = File.ReadAllText(jsonPath);
        int[] Arr(string key) => Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)").Groups.Cast<Group>().Skip(1).Select(g => int.Parse(g.Value)).ToArray();
        int Num(string key) => int.Parse(Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)").Groups[1].Value);
        var size = Arr("frameSize"); var pivot = Arr("pivot");
        int fw = size[0], fh = size[1], frames = Num("frames"), cols = Num("columns"), fps = Num("fps");

        var ti = (TextureImporter)AssetImporter.GetAtPath(png);
        ti.GetSourceTextureWidthAndHeight(out int texW, out int texH);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.spritePixelsPerUnit = 100f;
        ti.maxTextureSize = Mathf.Max(ti.maxTextureSize, Mathf.NextPowerOfTwo(Mathf.Max(texW, texH)));
        var piv = new Vector2(pivot[0] / (float)fw, 1f - pivot[1] / (float)fh);
        var metas = new SpriteMetaData[frames];
        for (int i = 0; i < frames; i++)
        {
            int c = i % cols, r = i / cols;
            metas[i] = new SpriteMetaData { name = $"{name}_{i}", rect = new Rect(c * fw, texH - (r + 1) * fh, fw, fh), alignment = (int)SpriteAlignment.Custom, pivot = piv };
        }
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
        ti.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        if (sprites.Length == 0) { fxLog.Add("cắt sprite thất bại: " + png); return null; }

        if (!AssetDatabase.IsValidFolder("Assets/Animation")) AssetDatabase.CreateFolder("Assets", "Animation");
        if (!AssetDatabase.IsValidFolder("Assets/Animation/PixelFX")) AssetDatabase.CreateFolder("Assets/Animation", "PixelFX");
        var clip = new AnimationClip { frameRate = fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = sprites.Select((s, i) => new ObjectReferenceKeyframe { time = i / (float)fps, value = s }).ToList();
        keys.Add(new ObjectReferenceKeyframe { time = sprites.Length / (float)fps, value = sprites[0] });
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        var st = AnimationUtility.GetAnimationClipSettings(clip); st.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, st);   // hiệu ứng môi trường: lặp
        AssetDatabase.CreateAsset(clip, FxAnimDir + name + ".anim");
        var ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(FxAnimDir + name + ".controller", clip);

        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprites[0];
        var unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (unlit != null) sr.sharedMaterial = unlit;          // lửa, phép tự phát sáng: không bị Light2D làm tối
        sr.sortingOrder = order;
        var an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        an.cullingMode = AnimatorCullingMode.CullCompletely;
        go.AddComponent<AnimRandomStart>();
        if (lightColor.HasValue)
        {
            var lgo = new GameObject("Light");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3(0, 0.4f, 0);
            var l = lgo.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = lightColor.Value; l.intensity = intensity;
            l.pointLightInnerRadius = 0.3f; l.pointLightOuterRadius = radius;
            l.falloffIntensity = 0.4f; l.shadowsEnabled = false;
            var so = new SerializedObject(l);                                         // chiếu lên mọi sorting layer
            var layers = so.FindProperty("m_ApplyToSortingLayers");
            if (layers != null)
            {
                var all = SortingLayer.layers; layers.arraySize = all.Length;
                for (int i = 0; i < all.Length; i++) layers.GetArrayElementAtIndex(i).intValue = all[i].id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var fso = new SerializedObject(lgo.AddComponent<LightFlicker2D>());
            fso.FindProperty("amplitude").floatValue = 0.15f; fso.FindProperty("speed").floatValue = 5f;
            fso.ApplyModifiedPropertiesWithoutUndo();
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        fxLog.Add($"tạo prefab mới {name} ({frames} khung {fw}×{fh})");
        return prefab;
    }
}
