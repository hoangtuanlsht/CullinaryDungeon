using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : Character
{
    [SerializeField] private float attackRange;
    [SerializeField] private float moveSpeed;
    [SerializeField] private Rigidbody2D rb;
    private void Update()
    {
        if(currentState != null)
        {
            currentState.OnExecute(this);
        }
    }
    public override void OnInit()
    {
        base.OnInit();

        ChangeState(new IdleState());
    }
    public override void OnDespawn()
    {
        base.OnDespawn();
    }
    public override void OnDeath()
    {
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

    }
    public bool isAttackInRange()
    {
         return false;
    }   
}
