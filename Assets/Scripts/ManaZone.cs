using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManaZone : MonoBehaviour
{
    public float manaRegenRate = 10f; // Mana regen per second
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Player player = collision.GetComponent<Player>();
            if (player != null)
            {
                player.PlayerMana.AddMana(Time.deltaTime * manaRegenRate); // hồi full mana khi vào zone
            }
        }
    }
}
