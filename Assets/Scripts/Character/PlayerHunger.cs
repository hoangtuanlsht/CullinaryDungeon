using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum HungerState
{
    Full,
    Normal,
    Hungry,
    Starving
}
[Serializable]
public class PlayerHunger 
{
    [SerializeField]private float hungerLevel = 100f;
    [SerializeField]private float hungerMaxLevel = 100f;
    [SerializeField]private float hungerSpeed;
    private HungerState hungerState;

    private int lastReportedLevel = -1;

    private int pauseRequests;
    public bool IsPaused => pauseRequests > 0;

    public void RequestPause()
    {
        pauseRequests++;
    }
    public void ReleasePause()
    {
        pauseRequests--;
        if (pauseRequests < 0)
        {
            pauseRequests = 0;
        }
    }
    public float HungerLevel => hungerLevel;
    public float HungerMaxLevel => hungerMaxLevel;
    public HungerState HungerState => hungerState;
    public event Action<HungerState> OnHungerStateChanged;
    public event Action<float,float> OnHungerLevelChanged;

    private void UpdateHungerState()
    {
        HungerState newState;
        if (hungerLevel >= hungerMaxLevel*0.75)
        {
            newState = HungerState.Full;
        }
        else if (hungerLevel >= hungerMaxLevel*0.5)
        {
            newState = HungerState.Normal;
        }
        else if (hungerLevel >= hungerMaxLevel*0.25)
        {
            newState = HungerState.Hungry;
        }
        else
        {
            newState = HungerState.Starving;
        }
        int newHungerLevel = Mathf.RoundToInt(hungerLevel);
        if (newHungerLevel != lastReportedLevel)
        {
            lastReportedLevel = newHungerLevel;
            OnHungerLevelChanged?.Invoke(newHungerLevel,hungerMaxLevel);
        }
        if (newState != hungerState)
        {
            hungerState = newState;
            OnHungerStateChanged?.Invoke(hungerState);
        } 
    }
    public void UpdateHungerLevel(float deltaTime)
    {
        if (IsPaused) return;
        hungerLevel -= hungerSpeed * deltaTime;
        hungerLevel = Mathf.Clamp(hungerLevel, 0f, hungerMaxLevel);
        UpdateHungerState();
    }
    public void EatFood(float food)
    {
        if(food < 0)
        {
            return;
        }
        if (hungerLevel + food > hungerMaxLevel)
        {
            hungerLevel = hungerMaxLevel;
        }
        else
        {
            hungerLevel += food;
        }
        UpdateHungerState();
    }
    public void AtLeastHunger(float ratio)
    {
        hungerLevel = Mathf.Max(hungerLevel, ratio*hungerMaxLevel);
        UpdateHungerState();
    }
    /// [Module 10] Nạp từ save. Ép HUD cập nhật dù số mới trùng số cũ.
    public void Restore(float level)
    {
        hungerLevel = Mathf.Clamp(level, 0f, hungerMaxLevel);
        lastReportedLevel = -1;
        UpdateHungerState();
    }
    public bool IsFull => hungerLevel >= hungerMaxLevel;
}
