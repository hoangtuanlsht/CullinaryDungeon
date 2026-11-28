using UnityEngine;

[CreateAssetMenu (fileName = "Skill_", menuName = "Culinaria/Skill Data")]
public class SkillData : ScriptableObject
{
    [SerializeField] private string skillID;
    [SerializeField] private string skillName;
    [SerializeField] private Sprite skillIcon;
    [SerializeField,Min(0f)] private float manaSpend = 25f;
    [SerializeField,Min(0f)] private float coolDown = 0.6f;
    [SerializeField,Min(0f)] private float castDuration = 0.35f;
    [SerializeField] private string animTrigger;
    [SerializeField] private SwordWave projectilePrefab;


    public string SkillID => skillID;
    public string SkillName => skillName;
    public Sprite SkillIcon => skillIcon;
    public float ManaSpend => manaSpend;
    public float CoolDown => coolDown;
    public float CastDuration => castDuration;
      
    public string AnimTrigger => animTrigger;  
    public SwordWave ProjectilePrefab => projectilePrefab;
}
