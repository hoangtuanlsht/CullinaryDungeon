using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Enemy : Character
{
    [SerializeField] private float attackRange;
    [SerializeField] private float moveSpeed;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Flame thrownPrefab;
    [SerializeField] private Transform thrownPoint;
    [SerializeField] private GameObject attackArea;


    private bool isRight = true;   

    private Character target;
    public Character Target => target;
    private void Update()
    {
        if(currentState != null && !IsDead)
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
        ChangedAnim("Attack");
        Throw();
        ActiveAttack();
        Invoke("DeactiveAttack", 0.5f);
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
        attackArea.SetActive(true);
    }
    private void DeactiveAttack()
    {
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
}
