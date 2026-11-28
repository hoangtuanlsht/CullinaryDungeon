using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Culinaria/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    [SerializeField] private List<SkillData> skills = new ();
    private Dictionary<string, SkillData> byId;
    
    private void OnEnable()
    {
        byId = null;
    }
    public bool TryGet(string id, out SkillData skill)
    {
        skill = null;
        if (string.IsNullOrEmpty(id)) return false;
        if (byId == null) Build();
        return byId.TryGetValue(id, out skill);
    }
    private void Build()
    {
        byId = new Dictionary<string, SkillData>(skills.Count);
        foreach (SkillData s in skills)
        {
            if (s == null || string.IsNullOrEmpty(s.SkillID)) continue;
            if (!byId.TryAdd(s.SkillID, s))
                Debug.LogError($"SkillDatabase: trùng ID '{s.SkillID}' ({byId[s.SkillID].name} và {s.name})", s);
        }
    }
#if UNITY_EDITOR
    [ContextMenu("Kiểm tra Id")]
    private void CheckIds()
    {
        var seen = new Dictionary<string, SkillData>();
        int errors = 0;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillData s = skills[i];
            if (s == null)
            {
                Debug.LogError($"SkillDatabase: ô #{i} trong danh sách bị trống", this);
                errors++;
            }
            else if (string.IsNullOrEmpty(s.SkillID))
            {
                Debug.LogError($"SkillDatabase: '{s.name}' chưa có ID", s);
                errors++;
            }
            else if (seen.TryGetValue(s.SkillID, out SkillData other))
            {
                Debug.LogError($"SkillDatabase: trùng ID '{s.SkillID}' ('{s.name}' và '{other.name}')", s);
                errors++;
            }
            else seen.Add(s.SkillID, s);
        }
        Debug.Log(errors == 0 ? $"SkillDatabase: {skills.Count} skill, không lỗi" : $"SkillDatabase: {errors} lỗi");
    }
#endif
}
