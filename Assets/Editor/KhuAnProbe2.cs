using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Chỉ đọc: loại tile, material của tilemap, nền và MapFX của phòng mẹ ở từng tầng (phục vụ trang trí khu ẩn).
public static class KhuAnProbe2
{
    static readonly Dictionary<string, string[]> Parents = new Dictionary<string, string[]>
    {
        { "FirstFloorScene 1", new[] { "F1_R03", "F1_R01" } },
        { "SecondFloorScene_v2", new[] { "F2_R09", "F2_R01" } },
        { "ThirdFloorScene", new[] { "F3_R09", "F3_R05" } },
        { "FourthFloorScene", new[] { "F4_R24", "F4_R18" } },
        { "FifthFloorScene", new[] { "F5_R16", "F5_R25" } },
        { "SixthFloorScene", new[] { "F6_R22", "F6_R18" } },
    };

    [MenuItem("Tools/Culinaria/Khu An/Probe 2 - tile va nen (chi doc)")]
    public static void Probe()
    {
        var sb = new StringBuilder();
        foreach (var path in KhuAnProbe.Scenes)
        {
            Scene s = SceneManager.GetSceneByPath(path); bool opened = false;
            try
            {
                if (!s.isLoaded) { s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened = true; }
                sb.AppendLine("=== " + s.name);
                var roots = s.GetRootGameObjects();
                foreach (var tm in roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)))
                {
                    if (!tm.gameObject.activeInHierarchy) continue;
                    var tr = tm.GetComponent<TilemapRenderer>();
                    var counts = new Dictionary<string, int>();
                    foreach (var p in tm.cellBounds.allPositionsWithin)
                    {
                        var t = tm.GetTile(p); if (t == null) continue;
                        string k = t.GetType().Name + ":" + t.name;
                        counts[k] = counts.TryGetValue(k, out var n) ? n + 1 : 1;
                    }
                    if (counts.Count == 0) continue;
                    sb.AppendLine($"TM {Path(tm.transform)} mat={(tr && tr.sharedMaterial ? tr.sharedMaterial.name : "-")} order={(tr ? tr.sortingOrder : 0)} layer={(tr ? tr.sortingLayerName : "")} col={(tm.GetComponent<TilemapCollider2D>() != null)} top: " +
                        string.Join(", ", counts.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key + "×" + kv.Value)));
                }
                foreach (var rn in Parents[s.name])
                {
                    var room = roots.SelectMany(r => r.GetComponentsInChildren<Room>(true)).FirstOrDefault(r => r.name == rn);
                    if (room == null) { sb.AppendLine("ROOM " + rn + " ?"); continue; }
                    var bg = new SerializedObject(room).FindProperty("background").objectReferenceValue as GameObject;
                    if (bg != null)
                    {
                        sb.AppendLine($"BG {rn}: {Path(bg.transform)} pos={bg.transform.position} scale={bg.transform.lossyScale}");
                        foreach (var sr in bg.GetComponentsInChildren<SpriteRenderer>(true).Take(6))
                            sb.AppendLine($"   SR {sr.name} sprite={(sr.sprite ? sr.sprite.name : "-")} pos={sr.transform.position} scale={sr.transform.lossyScale} size={sr.bounds.size} mat={(sr.sharedMaterial ? sr.sharedMaterial.name : "-")} draw={sr.drawMode} order={sr.sortingOrder} col={sr.color}");
                    }
                    else sb.AppendLine($"BG {rn}: (không có)");
                    var fx = room.transform.Find("MapFX");
                    if (fx != null)
                        foreach (Transform ch in fx)
                            sb.AppendLine($"   FX {ch.name} pos={ch.position} scale={ch.lossyScale}");
                }
                // vật trang trí gần phòng mẹ (sprite không thuộc nền)
                var mainRoom = roots.SelectMany(r => r.GetComponentsInChildren<Room>(true)).FirstOrDefault(r => r.name == Parents[s.name][0]);
                if (mainRoom != null)
                {
                    var b = mainRoom.GetComponent<Collider2D>().bounds;
                    var deco = roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                        .Where(sr => sr.gameObject.activeInHierarchy && b.Contains(new Vector3(sr.transform.position.x, sr.transform.position.y, b.center.z)) && Mathf.Abs(sr.transform.position.z) < 1f && sr.sprite != null)
                        .GroupBy(sr => sr.sprite.name).OrderByDescending(g => g.Count()).Take(15);
                    sb.AppendLine("DECO " + mainRoom.name + ": " + string.Join(", ", deco.Select(g => $"{g.Key}×{g.Count()}[{Path(g.First().transform.parent)}|{g.First().sharedMaterial?.name}|o{g.First().sortingOrder}|s{g.First().transform.lossyScale.x:0.##}]")));
                }
                var lights = roots.SelectMany(r => r.GetComponentsInChildren<Component>(true)).Where(c => c != null && c.GetType().Name == "Light2D").ToList();
                sb.AppendLine($"Light2D: {lights.Count} (global: {string.Join(",", lights.Where(l => l.GetType().GetProperty("lightType")?.GetValue(l)?.ToString() == "Global").Select(l => Path(l.transform)))})");
            }
            catch (System.Exception e) { sb.AppendLine("LỖI " + e.Message); }
            finally { if (opened && s.isLoaded) EditorSceneManager.CloseScene(s, true); }
        }
        string all = sb.ToString().Replace("\r", "").Replace("\n", " ¦ ");
        System.IO.File.WriteAllText("Temp/KhuAnProbe2.txt", sb.ToString());
        for (int i = 0; i < all.Length; i += 3000) Debug.Log("[KhuAnProbe2] " + all.Substring(i, Mathf.Min(3000, all.Length - i)));
    }

    static string Path(Transform t) => t == null ? "-" : t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
