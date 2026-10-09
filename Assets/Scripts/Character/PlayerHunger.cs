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
    private int lastReportedLevel = -1;
    public static event Action<HungerState> OnHungerStateChanged;
    public static event Action<float> OnHungerLevelChanged;
    private HungerState hungerState;

    private void UpdateHungerState()
    {
        HungerState newState;
        if (hungerLevel >= 75f)
        {
            newState = HungerState.Full;
        }
        else if (hungerLevel >= 50f)
        {
            newState = HungerState.Normal;
        }
        else if (hungerLevel >= 25f)
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
            OnHungerLevelChanged?.Invoke(newHungerLevel);
        }
        if (newState != hungerState)
        {
            hungerState = newState;
            OnHungerStateChanged?.Invoke(hungerState);
        } 
    }
    public void UpdateStarving(float deltaTime)
    {
        hungerLevel -= hungerSpeed * deltaTime;
        hungerLevel = Mathf.Clamp(hungerLevel, 0f, hungerMaxLevel);
        UpdateHungerState();
    }
    public void UpdateHungerLevel(float food)
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
}
