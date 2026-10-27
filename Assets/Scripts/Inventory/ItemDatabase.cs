using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Data/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemItem> items = new();
    private Dictionary<string, ItemItem> byId;

    private void OnEnable() => byId = null;
    public bool TryGet(string id, out ItemItem item)
    {
        item = null;
        if (string.IsNullOrEmpty(id)) return false;
        if (byId == null) Build();
        return byId.TryGetValue(id, out item);
    }
    private void Build()
    {
        byId = new Dictionary<string, ItemItem>(items.Count);
        foreach (ItemItem it in items)
        {
            if (it == null || string.IsNullOrEmpty(it.ItemId)) continue;
            if (!byId.TryAdd(it.ItemId, it))
                Debug.LogError($"ItemDatabase: trùng ID '{it.ItemId}' ({byId[it.ItemId].name} và {it.name})", it);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Module 10/Gom mọi item trong project")]
    private void CollectAll()
    {
        items.Clear();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ItemItem"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var it = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemItem>(path);
            if(it != null) items.Add(it);
        }
        items.Sort((a,b) => string.CompareOrdinal(a.name,b.name));
        byId = null;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"ItemDatabase: gom được {items.Count} item");
        Validate();
    }

    [ContextMenu ("Module 10/Kiểm tra Id")]
    private void Validate()
    {
        var seen = new Dictionary<string, ItemItem>();
        int errors = 0;
        foreach(ItemItem it in items)
        {
            if (it == null) continue;
            if (string.IsNullOrEmpty(it.ItemId))
            {
                Debug.LogError($"Item '{it.name}' chưa có Id", it);
                errors++;
            }
            else if(seen.TryGetValue(it.ItemId,out ItemItem other))
            {
                Debug.LogError($"Trùng ID '{it.ItemId}': '{it.name}' và '{other.name}'",it);
                errors++;
            }
            else seen.Add(it.ItemId, it);
        }
        Debug.Log(errors == 0 ? $"ItemDatabase: {items.Count} items, không lỗi" : $"ItemDatabase: {errors} lỗi");

    }
    [ContextMenu("Module 10/Điền Id còn trống từ tên file (chạy một lần)")]
    private void FillEmptyIds()
    {
        int n = 0;
        foreach (ItemItem it in items)
        {
            if (it == null || !string.IsNullOrEmpty(it.ItemId)) continue;
            it.EditorSetId("item_" + it.name.Trim().ToLowerInvariant().Replace(' ', '_'));
            UnityEditor.EditorUtility.SetDirty(it);
            n++;
        }
        UnityEditor.AssetDatabase.SaveAssets();
        byId = null;
        Debug.Log($"Đã điền {n} ID");
        Validate();
    }
#endif
}
