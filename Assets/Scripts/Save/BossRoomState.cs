using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 10] Đặt trên một Empty LUÔN BẬT trong phòng boss.
/// Boss đã hạ → tắt boss + trigger + thanh máu, bật lối đi; KHÔNG gọi OnDeath (sẽ thưởng lại).
public class BossRoomState : MonoBehaviour
{
    [SerializeField] private DefeatFlag boss;
    [Tooltip("Boss, trigger SpawnBoss, thanh máu boss, tường chắn (limitmap)")]
    [SerializeField] private GameObject[] disableIfDefeated;
    [Tooltip("nextMapPrefab, backMapPrefab")]
    [SerializeField] private GameObject[] enableIfDefeated;

    private void Start()
    {
        if (boss == null || !WorldProgress.HasFlag(boss.FlagId)) return;
        foreach(GameObject go in disableIfDefeated) if(go != null) go.SetActive(false);
        foreach(GameObject go in enableIfDefeated) if(go!= null) go.SetActive(true);
    }
}
