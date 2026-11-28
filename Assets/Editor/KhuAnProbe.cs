using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Chỉ đọc: liệt kê phòng (Room), tilemap có va chạm, SceneDoor, rương, sàn nứt, checkpoint của từng scene tầng.
/// Mở scene kiểu Additive rồi đóng lại, KHÔNG lưu. Kết quả ghi ra Temp/KhuAnProbe.txt và Console.
public static class KhuAnProbe
{
    public static readonly string[] Scenes = {
        "Assets/Scenes/FirstFloorScene 1.unity", "Assets/Scenes/SecondFloorScene_v2.unity", "Assets/Scenes/ThirdFloorScene.unity",
        "Assets/Scenes/FourthFloorScene.unity", "Assets/Scenes/FifthFloorScene.unity", "Assets/Scenes/SixthFloorScene.unity" };

    [MenuItem("Tools/Culinaria/Khu An/Probe (chi doc)")]
    public static void Probe()
    {
        var sb = new StringBuilder();
        foreach (var path in Scenes)
        {
            Scene s = SceneManager.GetSceneByPath(path);
            bool opened = false;
            try
            {
                if (!s.isLoaded) { s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened = true; }
                ProbeScene(s, sb);
            }
            catch (System.Exception e) { sb.AppendLine("LỖI " + path + ": " + e.Message); }
            finally { if (opened && s.isLoaded) EditorSceneManager.CloseScene(s, true); }
        }
        System.IO.File.WriteAllText("Temp/KhuAnProbe.txt", sb.ToString());
        Debug.Log("[KhuAnProbe] Ghi " + sb.Length + " ký tự vào Temp/KhuAnProbe.txt");
        string all = sb.ToString().Replace("\r", "").Replace("\n", " ¦ ");
        for (int i = 0; i < all.Length; i += 3000) Debug.Log("[KhuAnProbe] " + all.Substring(i, Mathf.Min(3000, all.Length - i)));
    }

    static void ProbeScene(Scene s, StringBuilder sb)
    {
        sb.AppendLine("=== " + s.name);
        sb.AppendLine("Roots: " + string.Join(", ", s.GetRootGameObjects().Select(g => g.name + (g.activeSelf ? "" : "(off)"))));
        foreach (var root in s.GetRootGameObjects())
        {
            foreach (var room in root.GetComponentsInChildren<Room>(true))
            {
                var poly = room.GetComponent<PolygonCollider2D>();
                string b = poly != null ? RectStr(poly.bounds) : "?";
                sb.AppendLine($"ROOM {room.name}{(room.gameObject.activeInHierarchy ? "" : "(off)")} {b}");
            }
            foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
            {
                var col = tm.GetComponent<TilemapCollider2D>();
                if (col == null) continue;
                tm.CompressBounds();
                var cb = tm.cellBounds;
                Grid g = tm.layoutGrid;
                string grid = g != null ? $"cell={g.cellSize.x} gridPos={g.transform.position}" : "noGrid";
                sb.AppendLine($"TM {GetPath(tm.transform)} on={tm.gameObject.activeInHierarchy} layer={LayerMask.LayerToName(tm.gameObject.layer)} tag={tm.tag} trig={col.isTrigger} cells=({cb.xMin},{cb.yMin})..({cb.xMax},{cb.yMax}) n={tm.GetUsedTilesCount()} {grid} scale={tm.transform.lossyScale.x}");
            }
            foreach (var d in root.GetComponentsInChildren<SceneDoor>(true))
            {
                var so = new SerializedObject(d);
                var ts = so.FindProperty("targetScene");
                var pn = ts != null ? ts.FindPropertyRelative("sceneName") : null;
                var ri = so.FindProperty("requireInteract");
                sb.AppendLine($"DOOR {d.DoorId} at {V(d.transform.position)} -> {(pn != null ? pn.stringValue : "?")}:{so.FindProperty("targetDoorId").stringValue} interact={(ri != null && ri.boolValue)}");
            }
            foreach (var c in root.GetComponentsInChildren<Chest>(true)) sb.AppendLine($"CHEST {c.name} at {V(c.transform.position)}");
            foreach (var c in root.GetComponentsInChildren<BreakableFloor>(true)) sb.AppendLine($"GP {c.name} at {V(c.transform.position)}");
            foreach (var c in root.GetComponentsInChildren<CheckPoint>(true)) sb.AppendLine($"CP {c.name} at {V(c.transform.position)}");
        }
    }

    static string V(Vector3 v) => $"({v.x:0.#},{v.y:0.#})";
    static string RectStr(Bounds b) => $"x[{b.min.x:0.#}..{b.max.x:0.#}] y[{b.min.y:0.#}..{b.max.y:0.#}]";
    static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
}
