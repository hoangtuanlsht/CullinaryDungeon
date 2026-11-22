using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Nền 3 lớp cho phòng boss tầng 3 (khung gốc F3_R18): xa / giữa (phòng triệu hồi) / gần (cột gãy, trong suốt).
/// Đặt các lớp làm con của nhóm nền mà Room.background đang trỏ tới (bật/tắt theo phòng), mỗi lớp một độ sâu Z.
/// Kích thước mỗi lớp tính theo camera perspective của phòng: lớp ở xa phải to hơn để phủ kín khung hình
/// trên cả quãng camera di chuyển (vùng CamBounds), và ảnh có "sàn vẽ sẵn" được hạ thấp để sàn vẽ không lộ
/// cao hơn sàn thật. Ảnh boss.png cũ được TẮT (không xóa). Chạy lại được.
/// Chạy: Tools > Culinaria > F3 Boss Background Layers.
public static class F3BossBackground
{
    const string SceneName = "ThirdFloorScene";
    const string BossRectId = "F3_R18";
    const string Dir = "Assets/BackGround/ThirdFloor/";
    const string Prefix = "BossLayer_";

    // tên, file, khoảng cách tới camera (đơn vị thế giới), sorting order, tỉ lệ chiều cao của đường sàn vẽ sẵn tính từ đáy ảnh (-1 = không có)
    static readonly (string name, string file, float dist, int order, float floorFrac)[] Layers =
    {
        ("Far",  "F3_BossFar.png",  52f,   -25, -1f),
        ("Mid",  "F3_BossMid.png",  37.4f, -20, 0.15f),
        ("Near", "F3_BossNear.png", 27f,   -10, 0.085f),
    };
    const float FloorY = 73f;       // mặt sàn đứng của phòng boss
    const float Margin = 1.04f;     // dư 4% cho màn hình rộng hơn 16:9 một chút
    const float Aspect = 16f / 9f;

    [MenuItem("Tools/Culinaria/F3 Boss Background Layers")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded) { EditorUtility.DisplayDialog("F3 Boss BG", "Hãy mở ThirdFloorScene trước.", "OK"); return; }

        // 1) Import sprite
        foreach (var l in Layers)
        {
            var imp = AssetImporter.GetAtPath(Dir + l.file) as TextureImporter;
            if (imp == null) { Debug.LogError("[F3BossBG] Thiếu " + Dir + l.file); return; }
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }

        // 2) Phòng boss, nhóm nền, camera
        var room = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true))
            .FirstOrDefault(r => F3RoomFrames.RoomRects(r).Any(x => x.id == BossRectId));
        if (room == null) { Debug.LogError("[F3BossBG] Không thấy phòng chứa khung " + BossRectId); return; }
        var so = new SerializedObject(room);
        var group = so.FindProperty("background").objectReferenceValue as GameObject;
        var vcam = so.FindProperty("vcam").objectReferenceValue as CinemachineVirtualCamera;
        if (group == null || vcam == null) { Debug.LogError($"[F3BossBG] {room.name}: thiếu background hoặc vcam"); return; }
        var ft = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
        float camDist = ft != null ? ft.m_CameraDistance : 18.7f;
        float halfH = vcam.m_Lens.Orthographic ? vcam.m_Lens.OrthographicSize : camDist * Mathf.Tan(vcam.m_Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfW = halfH * Aspect;

        // Quãng tâm camera = CamBounds thu vào nửa khung hình
        var cb = room.transform.Find("CamBounds");
        var b = (cb != null ? cb.GetComponent<Collider2D>() : room.GetComponent<Collider2D>()).bounds;
        float cx0 = b.min.x + halfW, cx1 = Mathf.Max(cx0, b.max.x - halfW);
        float cy0 = b.min.y + halfH, cy1 = Mathf.Max(cy0, b.max.y - halfH);

        // Dùng lại material + sorting layer của nền cũ (nếu project dùng ánh sáng 2D thì material mặc định có thể bị đen)
        var oldSr = group.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => !r.name.StartsWith(Prefix) && !r.transform.parent.name.StartsWith(Prefix));
        Debug.Log($"[F3BossBG] Material nền cũ: {(oldSr != null && oldSr.sharedMaterial != null ? oldSr.sharedMaterial.name + " / " + oldSr.sharedMaterial.shader.name : "(không có)")}, sorting layer {(oldSr != null ? oldSr.sortingLayerName : "-")}");

        // 3) Tắt nền cũ, xóa lớp tạo lần trước
        foreach (Transform t in group.transform.Cast<Transform>().ToList())
        {
            if (t.name.StartsWith(Prefix)) { Object.DestroyImmediate(t.gameObject); continue; }
            if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); Debug.Log($"[F3BossBG] Tắt nền cũ {t.name}"); }
        }

        // 4) Tạo lớp
        foreach (var l in Layers)
        {
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + l.file);
            float k = l.dist / camDist;                              // lớp xa thấy rộng hơn theo tỉ lệ này
            float needW = (cx1 - cx0) + 2f * halfW * k * Margin;
            float needX = (cx0 + cx1) / 2f;
            float visBottom = cy0 - halfH * k, visTop = cy1 + halfH * k;
            float s = spr.bounds.size.x, h0 = spr.bounds.size.y;
            float W = Mathf.Max(needW, (visTop - visBottom) * Margin * s / h0);
            float H = W * h0 / s;
            float bottom = (visBottom + visTop) / 2f - H / 2f;
            if (l.floorFrac >= 0f)
            {
                // Sàn vẽ sẵn không được hiện cao hơn sàn thật trong mọi vị trí camera còn nhìn thấy sàn thật.
                // Lớp xa trôi chậm hơn → trường hợp xấu nhất là camera cao nhất mà sàn thật vẫn còn ở mép dưới màn hình.
                float cWorst = Mathf.Clamp(FloorY + halfH, cy0, cy1);
                float maxFloor = cWorst + (FloorY - cWorst) * k;
                float floor = Mathf.Min(bottom + H * l.floorFrac, maxFloor);
                bottom = floor - H * l.floorFrac;
                if (bottom + H < visTop)                             // hạ thấp → phải to thêm để vẫn phủ kín phía trên
                {
                    H = (visTop - floor) / (1f - l.floorFrac);
                    W = H * s / h0;
                    bottom = floor - H * l.floorFrac;
                }
            }

            var go = new GameObject(Prefix + l.name);
            go.transform.SetParent(group.transform, false);
            go.transform.position = new Vector3(needX, bottom + H / 2f, l.dist - camDist);
            var ps = group.transform.lossyScale;
            go.transform.localScale = new Vector3(W / s / ps.x, H / h0 / ps.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr; sr.sortingOrder = l.order;
            if (oldSr != null) { sr.sharedMaterial = oldSr.sharedMaterial; sr.sortingLayerID = oldSr.sortingLayerID; sr.color = oldSr.color; }
            Debug.Log($"[F3BossBG] {go.name}: Z={l.dist - camDist:F1}, {W:F1}x{H:F1} ô, tâm ({needX:F1},{bottom + H / 2f:F1})");
        }
        Debug.Log($"[F3BossBG] Phòng {room.name}, camera dist {camDist}, nửa khung {halfW:F1}x{halfH:F1}, tâm camera x[{cx0:F1}..{cx1:F1}] y[{cy0:F1}..{cy1:F1}]");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
