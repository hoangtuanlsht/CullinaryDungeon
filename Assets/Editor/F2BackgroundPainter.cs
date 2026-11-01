// Đặt ảnh nền rừng phía sau 11 phòng tầng 2. Chạy: Tools > Culinaria > Paint Floor 2 Background.
// Ảnh nền được cắt sẵn từ 4 ảnh nền khu vực cũ của SecondFloorScene (SecondFloor, 2, map3, Map4),
// lưu ở Assets/BackGround/SecondFloor/F2Rooms. Mỗi ảnh vừa khít một phòng nên không tràn sang phòng khác.
// Chạy lại được: mỗi lần xoá và tạo lại F2_Background.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class F2BackgroundPainter
{
    const string Dir = "Assets/BackGround/SecondFloor/F2Rooms/";

    // tên phòng, x, y, rộng, cao, ảnh, độ sáng
    static readonly (string room, float x, float y, float w, float h, string img, float light)[] Rooms =
    {
        ("F2_R01",   0, -80, 18, 10, "F2_R01_bg.png",  0.90f), // map3: bìa rừng, cổng đá
        ("F2_R02",  18, -80, 36, 10, "F2_R02_bg.png",  0.90f), // 2: lối mòn, ánh sáng xuyên lá
        ("F2_R03",  54, -80, 36, 10, "F2_R03_bg.png",  0.90f), // SecondFloor: cầu treo, chân tháp
        ("F2_R04",  54, -70, 18, 20, "F2_R04_bg.png",  0.80f), // SecondFloor: cắt quanh ngọn tháp
        ("F2_R05",  90, -80, 36, 20, "F2_R05_bg.png",  0.90f), // 2 (lật): tán rừng lớn
        ("F2_R06", 108, -60, 18, 10, "F2_R06_bg.png",  0.85f), // Map4: điện thờ có nến
        ("F2_R07", 126, -80, 36, 10, "F2_R07_bg.png",  0.90f), // map3 (lật): cầu gỗ, nấm phát sáng
        ("F2_R08", 144, -70, 18, 20, "F2_R08_bg.png",  0.80f), // SecondFloor (lật): ngọn tháp B
        ("F2_R09",  36, -90, 36, 10, "F2_R09a_bg.png", 0.65f), // Map4: lối tắt âm u (nửa trái)
        ("F2_R09",  72, -90, 36, 10, "F2_R09b_bg.png", 0.65f), // Map4 lật: nửa phải, ghép đối xứng
        ("F2_R10", 162, -80, 18, 10, "F2_R10_bg.png",  0.85f), // Map4: tàn tích trước boss
        ("F2_R11", 180, -80, 36, 20, "F2_R11_bg.png",  0.90f), // map3 (lật): phòng boss, đổi khi có boss
    };

    [MenuItem("Tools/Culinaria/Paint Floor 2 Background")]
    public static void Paint()
    {
        string sortLayer = "Default"; int order = -20;
        var ground = GameObject.Find("Grid/Tilemap");
        if (ground != null)
        {
            var r = ground.GetComponent<Renderer>();
            if (r != null) { sortLayer = r.sortingLayerName; order = r.sortingOrder - 20; }
        }

        var old = GameObject.Find("F2_Background");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("F2_Background");
        Undo.RegisterCreatedObjectUndo(root, "Paint Floor 2 Background");

        int missing = 0;
        foreach (var rm in Rooms)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + rm.img);
            if (sprite == null) { missing++; Debug.LogWarning("Không tìm thấy sprite " + rm.img); continue; }
            var t = root.transform.Find("BG_" + rm.room);
            GameObject group;
            if (t != null) group = t.gameObject;
            else
            {
                group = new GameObject("BG_" + rm.room);
                group.transform.SetParent(root.transform, false);
            }
            Place(group, sprite, rm.x, rm.y, rm.w, rm.h, rm.light, sortLayer, order);
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[F2BackgroundPainter] Đã đặt nền cho tầng 2. Thiếu: " + missing);
    }

    static void Place(GameObject parent, Sprite s, float x, float y, float w, float h,
                      float light, string sortLayer, int order)
    {
        var go = new GameObject(s.name);
        go.transform.SetParent(parent.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = new Color(light, light, light, 1f);
        sr.sortingLayerName = sortLayer;
        sr.sortingOrder = order;
        var size = s.bounds.size;
        go.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
        // đặt tâm ảnh vào giữa khung (pivot của sprite có thể không ở giữa)
        go.transform.position = new Vector3(x + w / 2f, y + h / 2f, 0f)
                                - Vector3.Scale(s.bounds.center, go.transform.localScale);
    }
}
#endif
