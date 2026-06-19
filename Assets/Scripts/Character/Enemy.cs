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

    [SerializeField] private float knockbackForceX = 2f;
    
    protected bool isRight = true;
    protected bool isAttacking = false;
    protected bool isHurt = false;
    protected Character target;
    public Character Target => target;
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
        base.OnInit();

        ChangeState(new IdleState());
        DeactiveAttack();
    }
    public override void OnDespawn()
    {
        base.OnDespawn();
        Destroy(healthBar.gameObject);
        Destroy(gameObject);
        if (itemPrefab != null)
        {
            foreach (var item in itemPrefab)
            {
                Instantiate(item, transform.position, transform.rotation);
            }
        }
    }
    public override void OnHit(float damage)
    {
        base.OnHit(damage);
        if (!IsDead)
        {
            isHurt = true;

            rb.velocity = Vector2.zero;
            float knockbackDirection = transform.localScale.x > 0 ? -1f : 1f;
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
        // 1. Kiểm tra xem đã hết thời gian nghỉ chưa
        if (attackTimer > 0)
        {
            // Nếu chưa, có thể bắt nó đứng im (gọi anim Idle) để chờ
            ChangedAnim("Idle");
            return;
        }
        // 2. Nếu đã sẵn sàng, reset lại thời gian chờ
        attackTimer = attackCooldown;
        isAttacking = true;
        ChangedAnim("Attack");
        Throw();
        // 3. Thực hiện đòn tấn công như cũ=
        if (thrownPoint == null && thrownPrefab == null)
        {
            Invoke("ActiveAttack",attackTime);
            Invoke("DeactiveAttack", deAttackTime);
        }
    }
    private void Throw()
    {
        if(thrownPrefab != null && thrownPoint != null)
        {
            Debug.Log("Throw");
            Instantiate(thrownPrefab, thrownPoint.position, thrownPoint.rotation);
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
        // 1. Chọn màu sắc cho vòng tròn (Ví dụ: Màu đỏ)
        Gizmos.color = Color.red;

        // 2. Vẽ một vòng tròn khung (WireSphere) tại vị trí của nhân vật, với bán kính là attackRange
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
