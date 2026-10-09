using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class Character : MonoBehaviour
{
    [SerializeField]protected float maxhealth = 100f;
    protected float health;

    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action OnDied;
    public float Health => health;
    public float MaxHealth => maxhealth;

    private string currentAnimName;
    [SerializeField] private Animator anim;
    [SerializeField] protected HealthBar healthBar;
    

    public bool IsDead => health <= 0f;
    public void Start()
    {
        OnInit();
    }
    public virtual void OnDespawn()
    {
        // Clean up character properties
    }
    public void ChangedAnim(string animName)
    {
        if (currentAnimName != animName)
        {
            anim.ResetTrigger(animName);

            currentAnimName = animName;

            anim.SetTrigger(currentAnimName);
        }
    }
    public virtual void OnInit()
    {
        if (healthBar != null) healthBar.Bind(this);
        SetHealth(maxhealth);
    }
    protected void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, maxhealth);
        OnHealthChanged?.Invoke(health, maxhealth);
    }
    public virtual void OnHit(float damage, UnityEngine.Transform attacker)
    {
        if (IsDead) return;
        SetHealth(health - damage);
        Debug.Log($"{gameObject.name} hit with {damage} damage, remaining health: {health}");
        if (IsDead) OnDeath();
    }
    public virtual void OnDeath()
    {
        OnDied?.Invoke();
        ChangedAnim("Dead");
        Invoke(nameof(OnDespawn), 1.5f);
    }

}
