using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnBoss : MonoBehaviour
{
    public Boss_Slime bossSlimePrefab;
    public Boss_Minotaurus bossMinotaurusPrefab;
    public GruzMother gruzMotherPrefab;
    public GameObject healthBoss;

    public float healthBarDelay = 1.5f; 

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (bossSlimePrefab != null)
            {
                bossSlimePrefab.OnInit();
                bossSlimePrefab.gameObject.SetActive(true);
                StartCoroutine(ShowHealthBarDelayed());
            }
            else if (bossMinotaurusPrefab != null) 
            {
                bossMinotaurusPrefab.OnInit();
                bossMinotaurusPrefab.gameObject.SetActive(true);
                StartCoroutine(ShowHealthBarDelayed());
            }
            else if (gruzMotherPrefab != null)
            {
                gruzMotherPrefab.OnInit();
                gruzMotherPrefab.gameObject.SetActive(true);
                StartCoroutine(ShowHealthBarDelayed());
            }
        }
    }
    private System.Collections.IEnumerator ShowHealthBarDelayed()
    {
        yield return new WaitForSeconds(healthBarDelay);
        if (healthBoss != null)
        {
            healthBoss.SetActive(true);
        }
    }
}
