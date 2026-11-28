using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Nền nhiều lớp có chiều sâu cho các phòng tầng 3 chưa có nền ảnh (cùng cách tính với F3BossBackground):
///  - F3_R07 Hành lang kỵ sĩ (khung gốc R13–R15): xa / giữa / gần, lặp ngang, ô lẻ lật ngang để mép khớp.
///  - F3_R11 Hầm mộ nhện (R19): xa / giữa.   - F3_R08 Kho giáp (R16), F3_R12 Gác thượng (R20): 1 ảnh dọc.
/// Mỗi lớp: con "Layer_*" của nhóm nền mà Room.background trỏ tới (tạo BGGroup_<phòng> nếu chưa có và gán vào Room),
/// đặt ở Z = khoảng cách - 18.7, kích thước đủ phủ khung hình trên cả quãng camera di chuyển (vùng CamBounds).
/// Xóa tile nền gạch F3_BG_KnightDungeon trong lòng các phòng này (nó vẽ đè lên ảnh nền). Sao lưu scene trước khi chạy.
/// Chạy lại được. Chạy: Tools > Culinaria > F3 Room Backdrops.
public static class F3RoomBackdrops
{
    const string SceneName = "ThirdFloorScene";
    const string Dir = "Assets/BackGround/ThirdFloor/";
    const string Prefix = "Layer_";
    const string BackupPath = "Assets/Scenes/Backup/ThirdFloorScene_2026-11-26_beforeBackdrops.unity";
    const float Margin = 1.04f;
    const float Aspect = 16f / 9f;

    struct L
    {
        public string file; public float dist; public int order; public float floorFrac; public bool tileX;
        public L(string f, float d, int o, float ff = -1f, bool tx = false) { file = f; dist = d; order = o; floorFrac = ff; tileX = tx; }
    }

    // id khung gốc (nhận phòng theo hình học), mặt sàn đứng, các lớp
    static readonly (string rectId, float floorY, L[] layers)[] Rooms =
    {
        ("F3_R13", 73f, new[] { new L("F3_R07_Far.png", 52f, -25, -1f, true), new L("F3_R07_Mid.png", 37.4f, -20, -1f, true), new L("F3_R07_Near.png", 27f, -10, 0f, true) }),
        ("F3_R19", 73f, new[] { new L("F3_R11_Far.png", 52f, -25), new L("F3_R11_Mid.png", 37.4f, -20) }),
        ("F3_R16", 0f,  new[] { new L("F3_R08_Shaft.png", 37.4f, -20) }),
        ("F3_R20", 0f,  new[] { new L("F3_R12_Shaft.png", 37.4f, -20) }),
    };

    [MenuItem("Tools/Culinaria/F3 Room Backdrops")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded) { EditorUtility.DisplayDialog("F3 Backdrops", "Hãy mở ThirdFloorScene trước.", "OK"); return; }
        EditorSceneManager.SaveScene(scene);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null) { AssetDatabase.CopyAsset(scene.path, BackupPath); Debug.Log("[F3BG] Sao lưu: " + BackupPath); }

        foreach (var r in Rooms) foreach (var l in r.layers) Import(Dir + l.file);

        var roots = scene.GetRootGameObjects();
        var bgRoot = roots.FirstOrDefault(g => g.name == "______BackGround______");
        if (bgRoot == null) { Debug.LogError("[F3BG] Không thấy ______BackGround______"); return; }
        var template = bgRoot.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(s => s.sprite != null);
        var bgTiles = roots.SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).FirstOrDefault(t => t.name == "F3_BG_KnightDungeon");
        var rooms = roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)).ToList();

        foreach (var (rectId, floorY, layers) in Rooms)
        {
            var room = rooms.FirstOrDefault(rm => F3RoomFrames.RoomRects(rm).Any(q => q.id == rectId));
            if (room == null) { Debug.LogError("[F3BG] Không thấy phòng chứa khung " + rectId); continue; }
            var so = new SerializedObject(room);
            var bgProp = so.FindProperty("background");
            var vcam = so.FindProperty("vcam").objectReferenceValue as CinemachineVirtualCamera;
            if (bgProp == null || vcam == null) { Debug.LogError($"[F3BG] {room.name}: Room thiếu background/vcam"); continue; }

            // Nhóm nền của phòng
            var group = bgProp.objectReferenceValue as GameObject;
            if (group == null)
            {
                group = new GameObject("BGGroup_" + room.name);
                group.transform.SetParent(bgRoot.transform, false);
                group.transform.position = Vector3.zero;
                bgProp.objectReferenceValue = group;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(room);
            }
            foreach (Transform t in group.transform.Cast<Transform>().ToList())
            {
                if (t.name.StartsWith(Prefix)) { Object.DestroyImmediate(t.gameObject); continue; }
                if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); Debug.Log($"[F3BG] {room.name}: tắt nền cũ {t.name}"); }
            }

            // Xóa nền gạch dạng tile trong lòng các khung của phòng (tile 2x2 đặt ở ô p phủ ô p và p+1)
            int cleared = 0;
            if (bgTiles != null)
                foreach (var (_, rc) in F3RoomFrames.RoomRects(room))
                    for (int x = rc.xMin; x < rc.xMax; x++)
                        for (int y = rc.yMin; y < rc.yMax; y++)
                        {
                            var p = new Vector3Int(x, y, 0);
                            if (bgTiles.HasTile(p)) { bgTiles.SetTile(p, null); cleared++; }
                        }
            if (cleared > 0) EditorUtility.SetDirty(bgTiles);

            // Camera
            var ft = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
            float camDist = ft != null ? ft.m_CameraDistance : 18.7f;
            float halfH = vcam.m_Lens.Orthographic ? vcam.m_Lens.OrthographicSize : camDist * Mathf.Tan(vcam.m_Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * Aspect;
            var cb = room.transform.Find("CamBounds");
            var b = (cb != null ? cb.GetComponent<Collider2D>() : room.GetComponent<Collider2D>()).bounds;
            float cx0 = b.min.x + halfW, cx1 = b.max.x - halfW, cy0 = b.min.y + halfH, cy1 = b.max.y - halfH;
            if (cx1 < cx0) cx0 = cx1 = b.center.x;   // vùng hẹp hơn khung hình → Confiner giữ camera ở giữa
            if (cy1 < cy0) cy0 = cy1 = b.center.y;

            var log = new List<string>();
            foreach (var l in layers)
            {
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + l.file);
                if (spr == null) { Debug.LogError("[F3BG] Thiếu sprite " + l.file); continue; }
                float k = l.dist / camDist;
                float needW = (cx1 - cx0) + 2f * halfW * k * Margin;
                float needX = (cx0 + cx1) / 2f;
                float visBottom = cy0 - halfH * k, visTop = cy1 + halfH * k;
                float s = spr.bounds.size.x, h0 = spr.bounds.size.y;

                float H = (visTop - visBottom) * Margin;
                if (!l.tileX) H = Mathf.Max(H, needW * h0 / s);           // 1 ảnh: phủ cả bề ngang
                float bottom = (visBottom + visTop) / 2f - H / 2f;
                if (l.floorFrac >= 0f)
                {
                    float cWorst = Mathf.Clamp(floorY + halfH, cy0, cy1);
                    float maxFloor = cWorst + (floorY - cWorst) * k;
                    float floor = Mathf.Min(bottom + H * l.floorFrac, maxFloor);
                    bottom = floor - H * l.floorFrac;
                    if (bottom + H < visTop) { H = (visTop - floor) / (1f - l.floorFrac); bottom = floor - H * l.floorFrac; }
                }
                float W = H * s / h0;
                int n = l.tileX ? Mathf.Max(1, Mathf.CeilToInt(needW / W)) : 1;
                float start = needX - n * W / 2f;
                for (int i = 0; i < n; i++)
                {
                    var go = new GameObject($"{Prefix}{System.IO.Path.GetFileNameWithoutExtension(l.file)}_{i}");
                    go.transform.SetParent(group.transform, false);
                    go.transform.position = new Vector3(start + (i + 0.5f) * W, bottom + H / 2f, l.dist - camDist);
                    var ps = group.transform.lossyScale;
                    go.transform.localScale = new Vector3(W / s / ps.x, H / h0 / ps.y, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = spr; sr.sortingOrder = l.order;
                    sr.flipX = i % 2 == 1;                                   // ô lẻ lật ngang → mép nối khớp
                    if (template != null) { sr.sharedMaterial = template.sharedMaterial; sr.sortingLayerID = template.sortingLayerID; }
                }
                log.Add($"{l.file}: Z={l.dist - camDist:F1}, {n}×({W:F1}x{H:F1})");
            }
            Debug.Log($"[F3BG] {room.name} ({rectId}): nhóm {group.name}, xóa {cleared} tile nền gạch, camera x[{cx0:F1}..{cx1:F1}] y[{cy0:F1}..{cy1:F1}] | {string.Join(" | ", log)}");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void Import(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogError("[F3BG] Thiếu " + path); return; }
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 4096;
        imp.SaveAndReimport();
    }
}
