using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 9] Gắn vào prefab hiệu ứng hạt. Chạy xong thì tự trả về pool (hoặc tự Destroy nếu không đến từ pool).
[RequireComponent(typeof(ParticleSystem))]
public class ParticleAutoDespawn : MonoBehaviour, IPoolable
{
    private ParticleSystem ps;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.stopAction = ParticleSystemStopAction.Callback;   // prefab đang để Destroy; đổi sang Callback để về kho thay vì tự huỷ
    }

    public void OnSpawned()
    {
        ps.Clear(true);
        ps.Play(true);
    }

    public void OnDespawned()
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnParticleSystemStopped() => PoolManager.Despawn(gameObject);
}
