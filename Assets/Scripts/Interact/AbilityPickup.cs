using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 7] Chạm vào là nhận kỹ năng. Đặt trên GameObject có Collider2D (Is Trigger).
[RequireComponent(typeof(Collider2D))]
public class AbilityPickup : MonoBehaviour
{
    [SerializeField] private Ability ability;

    private string FlagId => "ability_" + ability;

    private void Start()
    {
        if (WorldProgress.HasFlag(FlagId)) gameObject.SetActive(false);   // đã nhặt, scene vừa load lại
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;

        player.Abilities.Unlock(ability);
        WorldProgress.SetFlag(FlagId);
        gameObject.SetActive(false);
    }
}
