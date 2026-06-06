using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    private float health;

    private string currentAnimName;
    [SerializeField] private Animator anim;
    [SerializeField] private HealthBar healthBar;

    public bool IsDead => health <= 0f;
    public void Start()
    {
        OnInit();
    }
    public virtual void OnInit()
    {
        health = 100f;
        healthBar.OnInit(100);
    }
    public virtual void OnDespawn()
    {
        // Clean up character properties
    }
    protected void ChangedAnim(string animName)
    {
        if (currentAnimName != animName)
        {
            anim.ResetTrigger(animName);

            currentAnimName = animName;

            anim.SetTrigger(currentAnimName);
        }
    }
    public void OnHit(float damage)
    {
        if (!IsDead)
        { 
            health -= damage;
            Debug.Log($"{gameObject.name} hit with {damage} damage, remaining health: {health}");
            if (health <= damage)
            {
                
                OnDeath();
            }
            healthBar.SetNewHP(health);

        }
    }
    public virtual void OnDeath()
    {
        ChangedAnim("Dead");
        Invoke(nameof(OnDespawn), 2f);
    }
    
}
