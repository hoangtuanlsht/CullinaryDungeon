using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Character : MonoBehaviour
{
    [SerializeField]private float maxhealth = 100f;
    protected float health;

    private string currentAnimName;
    [SerializeField] private Animator anim;
    [SerializeField] protected HealthBar healthBar;

    public bool IsDead => health <= 0f;
    public void Start()
    {
        OnInit();
    }
    public virtual void OnInit()
    {
        health = maxhealth;
     
        healthBar.OnInit(maxhealth,transform);
    }
    public virtual void OnDespawn()
    {
        // Clean up character properties
    }
    public void ChangedAnim(string animName)
    {
        Debug.Log("Anim: " + animName);
        if (currentAnimName != animName)
        {
            anim.ResetTrigger(animName);

            currentAnimName = animName;

            anim.SetTrigger(currentAnimName);
        }
    }
    public virtual void OnHit(float damage)
    {
        if (!IsDead)
        { 
            health -= damage;            
            Debug.Log($"{gameObject.name} hit with {damage} damage, remaining health: {health}");
            if (health <= damage)
            {
                health = 0;
                healthBar.SetNewHP(health);
                OnDeath();                
                return;
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
