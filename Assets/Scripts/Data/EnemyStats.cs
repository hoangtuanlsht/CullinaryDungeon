using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "Culinaria/Enemy Stats")]
public class EnemyStats : ScriptableObject
{
    [Header("Sinh tồn")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Di chuyển")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float knockbackForceX = 3f;

    [Header("Tấn công")]
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackTime = 0.3f;
    [SerializeField] private float deAttackTime = 0.8f;

    [Header("Rơi đồ khi chết")]
    [SerializeField] private List<ItemClass> drops = new List<ItemClass>();

    [Header("Chỉ dành cho Boss")]
    [Tooltip("Kỹ năng Player nhận sau khi hạ boss này. None = không thưởng.")]
    [SerializeField] private Ability rewardAbility = Ability.None;
    public Ability RewardAbility => rewardAbility;
    [SerializeField, Min(0f)] private float rewardPlayerDamage = 0f;

    // Chỉ mở getter: code gameplay KHÔNG thể ghi vào SO (compiler chặn).
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float KnockbackForceX => knockbackForceX;
    public float AttackRange => attackRange;
    public float Damage => damage;
    public float AttackCooldown => attackCooldown;
    public float AttackTime => attackTime;
    public float DeAttackTime => deAttackTime;
    public IReadOnlyList<ItemClass> Drops => drops;
    public float RewardPlayerDamage => rewardPlayerDamage;

#if UNITY_EDITOR
    // Chỉ dùng cho công cụ Editor ở Bước 2 (chép số từ prefab cũ sang). Không tồn tại trong bản build.
    internal void EditorSetValues(float maxHealth, float moveSpeed, float knockbackForceX,
        float attackRange, float damage, float attackCooldown, float attackTime,
        float deAttackTime, List<ItemClass> drops)
    {
        this.maxHealth = maxHealth;
        this.moveSpeed = moveSpeed;
        this.knockbackForceX = knockbackForceX;
        this.attackRange = attackRange;
        this.damage = damage;
        this.attackCooldown = attackCooldown;
        this.attackTime = attackTime;
        this.deAttackTime = deAttackTime;
        this.drops = drops != null ? new List<ItemClass>(drops) : new List<ItemClass>();
    }
#endif
}
