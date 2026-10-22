using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Enemy : Character
{
    [SerializeField] protected float attackRange;
    [SerializeField] protected float moveSpeed;
    [SerializeField] protected float attackTime= 0.3f;
    [SerializeField] protected float deAttackTime=0.8f;
    [SerializeField] public float damager;
    [SerializeField] protected Rigidbody2D rb;
    [SerializeField] protected Flame thrownPrefab;
    [SerializeField] protected Transform thrownPoint;
    [SerializeField] protected GameObject attackArea;
    [SerializeField] protected List<ItemClass> itemPrefab;
    [SerializeField] protected float attackCooldown = 2f; // Thời gian nghỉ giữa 2 đòn đánh (tính bằng giây)
    protected float attackTimer = 0f; // Biến đếm ngược thời gian

    [SerializeField] private float knockbackForceX = 3f;
    
    protected bool isRight = true;
    protected bool isAttacking = false;
    protected bool isHurt = false;
    protected Character target;
    public Character Target => target;

    [Header("Data (Module 4)")]
    [SerializeField] private EnemyStats stats;
    public EnemyStats Stats => stats;
    [Header("Hit")]
    private HitFeedback hitFeedback;
    private void Awake()
    {
        hitFeedback = GetComponent<HitFeedback>();
    }
    private void Update()
    {
        if (isHurt)
        {
            return;
        }
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
        if (currentState != null && !IsDead)
        {
            currentState.OnExecute(this);
        }
    }
    public override void OnInit()
    {
        ApplyStats();
        base.OnInit();

        ChangeState(new IdleState());
        DeactiveAttack();
        if (thrownPrefab != null)
        {
            PoolManager.Prewarm(thrownPrefab, 3);
            if (thrownPrefab.hitVFX != null)
            {
                PoolManager.Prewarm(thrownPrefab.hitVFX, 2);
            }
        }
    }
    public void ApplyStats()
    {
        if (stats != null)
        {
            maxhealth = stats.MaxHealth;
            moveSpeed = stats.MoveSpeed;
            knockbackForceX = stats.KnockbackForceX;
            attackRange = stats.AttackRange;
            damager = stats.Damage;
            attackCooldown = stats.AttackCooldown;
            attackTime = stats.AttackTime;
            deAttackTime = stats.DeAttackTime;
            itemPrefab = new List<ItemClass>(stats.Drops);
        }
    }
    public override void OnDespawn()
    {
        base.OnDespawn();
        Destroy(gameObject);
        if (itemPrefab != null)
        {
            foreach (var item in itemPrefab)
            {
                PoolManager.Spawn(item, transform.position, Quaternion.identity);
            }
        }
    }
    public override void OnHit(float damage,UnityEngine.Transform attacker)
    {
        base.OnHit(damage,attacker);
        if (hitFeedback  != null) hitFeedback.Play();
        if (!IsDead)
        {
            isHurt = true;

            rb.velocity = Vector2.zero;
            float knockbackDirection = transform.position.x < attacker.position.x ? -1f : 1f;
            rb.AddForce(new Vector2(knockbackDirection * knockbackForceX, 0), ForceMode2D.Impulse);
            Invoke("ResetHurt", 0.5f);
        }
    }
    public void ResetHurt()
    {
        isHurt= false;
    }
    public override void OnDeath()
    {      
        ChangeState(null);
        Destroy(healthBar.gameObject);
        base.OnDeath();

    }
    private IsState currentState;
    public void ChangeState(IsState newState)
    {
        if (currentState != null)
        {
            currentState.OnExit(this);
        }
        currentState = newState;
        if(currentState != null)
        {
            currentState.OnEnter(this);
        }
    }
    public void Moving()
    {
        ChangedAnim("Run");
        rb.velocity = transform.right * moveSpeed;
    }
    public void StopMoving()
    {
        ChangedAnim("Idle");
        rb.velocity = Vector2.zero;
    }
    public void Attack()
    {
        if (attackTimer > 0)
        {
            return;
        }
        if (Target != null)
        {
            bool isFaceRight = Target.transform.position.x > transform.position.x;

            if (isRight != isFaceRight)
            {
                ChangeDirection(isFaceRight);
            }
        }
        attackTimer = attackCooldown;
        isAttacking = true;
        ChangedAnim("Attack");
        Throw();
        
        if (thrownPoint == null && thrownPrefab == null)
        {
            Invoke("ActiveAttack",attackTime);
        }
        Invoke("DeactiveAttack", deAttackTime);
    }
    private void Throw()
    {
        if(thrownPrefab != null && thrownPoint != null)
        {
            PoolManager.Spawn(thrownPrefab, thrownPoint.position, thrownPoint.rotation);
        }

    }
    public bool isAttackInRange()
    {
        if (Target != null && Vector2.Distance(Target.transform.position, transform.position) <= attackRange)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    private void ActiveAttack()
    {
        if(attackArea == null)
        {
            return;
        }
        attackArea.SetActive(true);
    }
    private void DeactiveAttack()
    {
        isAttacking = false;
        if (attackArea == null)
        {
            return;
        }
        attackArea.SetActive(false);
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        
        if (collision.CompareTag("EnemyWall"))
        {
            ChangeDirection(!isRight);
        }
    }
    public void ChangeDirection(bool isRight)
    {
        if (isAttacking) return;
        this.isRight = isRight;
        transform.rotation = isRight ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(0, 180, 0);
    }

    internal void SetTarget(Character character)
    {
        this.target = character;
        if (isAttacking)
        {
            return;
        }
        if(isAttackInRange())
        {
            ChangeState(new AttackState());
        }
        else
        if(Target != null){
            ChangeState(new PatrolState());
        }
        else
        {
            ChangeState(new IdleState());
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    protected void GrantReward()
    {
        if (stats == null)
        {
            Debug.LogWarning($"{name}: chưa gán EnemyStats.", this);
            return;
        }
        Player p = FindObjectOfType<Player>();
        if (p == null) return;

        if (stats.RewardPlayerDamage > 0f) p.damage = stats.RewardPlayerDamage;
        p.Abilities.Unlock(stats.RewardAbility);       // None thì Unlock tự bỏ qua
    }
#if UNITY_EDITOR
    [ContextMenu("Module 4/New_EnemyStats_To_CurrentStats")]
    private void CreateStatsFromCurrentValues()
    {
        const string root = "Assets/Data";
        const string folder = root + "/Enemies";
        if (!UnityEditor.AssetDatabase.IsValidFolder(root))
            UnityEditor.AssetDatabase.CreateFolder("Assets", "Data");
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            UnityEditor.AssetDatabase.CreateFolder(root, "Enemies");

        EnemyStats asset = ScriptableObject.CreateInstance<EnemyStats>();
        asset.EditorSetValues(maxhealth, moveSpeed, knockbackForceX, attackRange, damager,
                              attackCooldown, attackTime, deAttackTime, itemPrefab);

        string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{folder}/{gameObject.name}_Stats.asset");
        UnityEditor.AssetDatabase.CreateAsset(asset, path);
        UnityEditor.AssetDatabase.SaveAssets();

        stats = asset;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"Đã tạo {path} và gán vào {name}", asset);
    }
#endif
}
