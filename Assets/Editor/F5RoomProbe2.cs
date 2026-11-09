using UnityEngine;
using UnityEditor;
using System.Text;
using System.Linq;

public static class F5RoomProbe2
{
    [MenuItem("Tools/Culinaria/F5 - Probe2 khung phòng")]
    static void Run()
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Debug.Log("[P2] Scene " + sc.name);
        foreach (var root in sc.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var cols = t.GetComponents<Collider2D>();
            if (cols.Length == 0 && t.GetComponent("CinemachineVirtualCamera") == null) continue;
            if (!(t.name.Contains("R0") || t.name.Contains("R1") || t.name.Contains("R2") || t.name.Contains("R3") || t.name.Contains("R4") || t.name.Contains("Cam") || (t.parent && (t.parent.name.Contains("_R") ))))
                continue;
            var sb = new StringBuilder("[P2] " + GetPath(t) + " layer=" + LayerMask.LayerToName(t.gameObject.layer) + " active=" + t.gameObject.activeInHierarchy + " comps=");
            foreach (var c in t.GetComponents<Component>()) sb.Append(c ? c.GetType().Name : "null").Append(',');
            foreach (var c in cols)
            {
                sb.Append(" | ").Append(c.GetType().Name).Append(" trig=").Append(c.isTrigger).Append(" en=").Append(c.enabled);
                if (c is PolygonCollider2D p)
                {
                    sb.Append(" pts[");
                    foreach (var v in p.points) { var w = t.TransformPoint(v + p.offset); sb.Append(w.x.ToString("0.#")).Append(',').Append(w.y.ToString("0.#")).Append(';'); }
                    sb.Append(']');
                }
                else if (c is BoxCollider2D b) sb.Append(" box c=").Append((Vector2)t.TransformPoint(b.offset)).Append(" s=").Append(b.size);
            }
            Debug.Log(sb.ToString());
        }
    }
    static string GetPath(Transform t) { string s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
}
