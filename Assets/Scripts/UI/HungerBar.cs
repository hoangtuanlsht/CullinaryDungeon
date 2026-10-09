using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HungerBar : MonoBehaviour
{
    [SerializeField] private Image imageFill;

    [SerializeField] private Player player;


    private void OnEnable()
    {
        player.PlayerHunger.OnHungerLevelChanged += HandleHungerLevelChanged;
        player.PlayerHunger.OnHungerStateChanged += HandleHungerStateChanged;
        HandleHungerLevelChanged(player.PlayerHunger.HungerLevel, player.PlayerHunger.HungerMaxLevel);
        HandleHungerStateChanged(player.PlayerHunger.HungerState);
    }

    private void OnDisable()
    {
        player.PlayerHunger.OnHungerLevelChanged -= HandleHungerLevelChanged;
        player.PlayerHunger.OnHungerStateChanged -= HandleHungerStateChanged;
    }
    public void HandleHungerLevelChanged(float current, float max)
    {
        imageFill.fillAmount = current / max;
    }
    public void HandleHungerStateChanged(HungerState state)
    {
        if(state == HungerState.Starving)
        {
            imageFill.color = Color.red;
        }
        else if(state == HungerState.Hungry)
        {
            imageFill.color = Color.yellow;
        }
        else if(state == HungerState.Normal)
        {
            imageFill.color = Color.blue;
        }
        else
        {
            imageFill.color = Color.green;
        }
    }
}

