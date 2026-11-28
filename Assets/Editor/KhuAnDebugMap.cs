using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Chỉ đọc: vẽ bản đồ ô quanh lối vào khu ẩn. # đất (tilemap va chạm), p bậc một chiều, B khối có collider (sàn nứt/ấn phong),
/// D DeadZone, . trống. Ghi ra Console.
public static class KhuAnDebugMap
{
    [MenuItem("Tools/Culinaria/Khu An/Debug - ban do o tang 2 quanh san nut")]
    public static void F2() => Dump(1, 56, 90, -152, -96);
    [MenuItem("Tools/Culinaria/Khu An/Debug - o (305,-91) tang 5")]
    public static void F5Cell()
    {
        var s = SceneManager.GetSceneByPath(KhuAnProbe.Scenes[4]); bool opened = false;
        if (!s.isLoaded) { s = EditorSceneManager.OpenScene(KhuAnProbe.Scenes[4], OpenSceneMode.Additive); opened = true; }
        var sb = new StringBuilder();
        foreach (var y in new[] { -89, -90, -91, -92 })
            foreach (var tm in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)))
            {
                var t = tm.GetTile(new Vector3Int(305, y, 0));
                if (t != null) sb.Append($"y{y}: {tm.name} ({t.name}, trigger={(tm.TryGetComponent<TilemapCollider2D>(out var c) ? c.isTrigger.ToString() : "-")}, tag={tm.tag}) ¦ ");
            }
        Debug.Log("[KhuAnMap] " + sb);
        if (opened) EditorSceneManager.CloseScene(s, true);
    }
    [MenuItem("Tools/Culinaria/Khu An/Debug - ban do o tang 4 quanh san nut")]
    public static void F4() => Dump(3, 86, 118, -182, -136);
    [MenuItem("Tools/Culinaria/Khu An/Debug - ban do o tang 5 quanh ham")]
    public static void F5() => Dump(4, 276, 345, -103, -76);

    public static void Dump(int sceneIdx, int x0, int x1, int y0, int y1)
    {
        string path = KhuAnProbe.Scenes[sceneIdx];
        var s = SceneManager.GetSceneByPath(path); bool opened = false;
        if (!s.isLoaded) { s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened = true; }
        try
        {
            Physics2D.SyncTransforms();
            var maps = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Tilemap>(false)).ToList();
            var solid = maps.Where(t => t.TryGetComponent<TilemapCollider2D>(out var c) && !c.isTrigger && !t.TryGetComponent<PlatformEffector2D>(out _) && !t.name.Contains("Plat")).ToList();
            var plat = maps.Where(t => t.TryGetComponent<TilemapCollider2D>(out var c) && !c.isTrigger && (t.TryGetComponent<PlatformEffector2D>(out _) || t.name.Contains("Plat"))).ToList();
            var dead = maps.Where(t => t.CompareTag("DeadZone")).ToList();
            var boxes = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BoxCollider2D>(false)).Where(b => !b.isTrigger && b.GetComponent<Tilemap>() == null).ToList();
            var sb = new StringBuilder();
            sb.Append("x0=" + x0 + " ¦ ");
            for (int y = y1; y >= y0; y--)
            {
                sb.Append(y.ToString().PadLeft(5)).Append(' ');
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector3Int(x, y, 0); var w = new Vector2(x + 0.5f, y + 0.5f);
                    char ch = '.';
                    if (boxes.Any(b => b.OverlapPoint(w))) ch = 'B';
                    else if (solid.Any(t => t.HasTile(p))) ch = '#';
                    else if (plat.Any(t => t.HasTile(p))) ch = 'p';
                    else if (dead.Any(t => t.HasTile(p))) ch = 'D';
                    sb.Append(ch);
                }
                sb.Append(" ¦ ");
            }
            var gp = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BreakableFloor>(true)).Select(b => $"{b.name} layer={LayerMask.LayerToName(b.gameObject.layer)} pos={b.transform.position} active={b.gameObject.activeInHierarchy}");
            sb.Append(" GP: " + string.Join("; ", gp));
            sb.Append(" PLATMAPS: " + string.Join(",", plat.Select(t => t.name)));
            Debug.Log("[KhuAnMap] " + sb);
        }
        finally { if (opened) EditorSceneManager.CloseScene(s, true); }
    }
}
