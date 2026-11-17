using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

[Serializable]
public class PlayerMana
{
    [SerializeField] private float manaLevel = 100f;    
    [SerializeField] private float manaMaxLevel = 100f;
    [SerializeField] private float manaRegenRate = 2f; // Mana regen per second
    private float lastReportedLevel = -1;

    public event Action<float, float> OnManaLevelChanged;
    public event Action OnManaNotEnough;


    public float ManaLevel => manaLevel;
    public float ManaMaxLevel => manaMaxLevel;
    public float ManaRegenRate => manaRegenRate;
    public bool IsFull => manaLevel >= manaMaxLevel;

    public void Regeneration(float deltaTime)
    {
        if(!IsFull)
        {
            manaLevel += manaRegenRate * deltaTime;
            manaLevel = Mathf.Clamp(manaLevel, 0, manaMaxLevel);
        }   
        ManaNotify();
    }
    private void ManaNotify()
    {
        int newManaLevel = Mathf.RoundToInt(manaLevel);
        if (newManaLevel != lastReportedLevel)
        {
            lastReportedLevel = newManaLevel;
            OnManaLevelChanged?.Invoke(newManaLevel, manaMaxLevel);
        }
    }
    public void AddMana(float amount)
    {
        if(amount <= 0) return;
        if (manaLevel + amount <= manaMaxLevel)
        {
            manaLevel += amount;
        }
        else
        {
            manaLevel = manaMaxLevel;
        }
        ManaNotify();

    }
    public bool TrySpend(float amount)
    {
        if(amount < 0) return false;
        if (manaLevel >= amount)
        {
            manaLevel -= amount;
            ManaNotify();
            return true;
        }
        else
        {
            Debug.Log("Not enough mana!");
            OnManaNotEnough?.Invoke();
            return false;
        }
    }
    public void Restore(float level)
    {
        manaLevel = Mathf.Clamp(level, 0, manaMaxLevel);
        lastReportedLevel = -1;
        ManaNotify();
    }
}
