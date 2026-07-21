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
        Destroy(gameObject);
        if (itemPrefab != null)
        {
            foreach (var item in itemPrefab)
            {
                Instantiate(item, transform.position, transform.rotation);
            }
        }
    }
    public override void OnHit(float damage,UnityEngine.Transform attacker)
    {
        base.OnHit(damage,attacker);
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
}
