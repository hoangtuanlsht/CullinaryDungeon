using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HungerSafeZone : MonoBehaviour
{
    private Player player;
    private bool holding;

    private void OnEnable()
    {
        player = FindObjectOfType<Player>();
        if (player == null) return;
        player.PlayerHunger.RequestPause();
        holding = true;
    }
    private void OnDisable()
    {
        if (!holding) return;
        player.PlayerHunger.ReleasePause();
        holding = false;
    }
}
