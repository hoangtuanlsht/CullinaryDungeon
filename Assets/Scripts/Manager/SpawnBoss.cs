using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnBoss : MonoBehaviour
{
    public Boss_Slime bossSlimePrefab;
    public Boss_Minotaurus bossMinotaurusPrefab;
    public GruzMother gruzMotherPrefab;
    public GameObject healthBoss;

    
    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (bossSlimePrefab != null)
            {
                bossSlimePrefab.OnInit();
                bossSlimePrefab.gameObject.SetActive(true);
                healthBoss.SetActive(true);
            }
            if (bossMinotaurusPrefab != null)
            {
                bossMinotaurusPrefab.OnInit();
                bossMinotaurusPrefab.gameObject.SetActive(true);
                healthBoss.SetActive(true);
            }
            if (gruzMotherPrefab != null)
            {
                gruzMotherPrefab.OnInit();
                gruzMotherPrefab.gameObject.SetActive(true);
                healthBoss.SetActive(true);
            }
        }
    }
}
