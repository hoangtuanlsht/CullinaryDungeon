using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    internal IObjectPool<PooledObject> Pool { get; private set; }
    internal bool IsInPool { get; set; }
    private IPoolable[] callbacks;

    internal void Init(IObjectPool<PooledObject> pool)
    {
        Pool = pool;
        callbacks = GetComponentsInChildren<IPoolable>(true);
      
    }
    internal void NotifySpawned()
    {
        foreach(IPoolable c in callbacks)
        {
            c.OnSpawned();
        }
    }
    internal void NotifyDespawned()
    {
        foreach(IPoolable c in callbacks)
        {
            c.OnDespawned();
        }
    }

}
