using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

/// [Module 9] Kho object dùng chung. Đặt MỘT cái trong PersistentGameplay.
/// Mỗi prefab có một kho riêng (ObjectPool của Unity). Object nằm làm con của PoolManager,
/// nên không bị huỷ khi tầng unload; thay vào đó, khi tầng unload, mọi object đang dùng được thu về.
public class PoolManager : Singleton<PoolManager>
{
    [Tooltip("Sức chứa ban đầu của mỗi kho.")]
    [SerializeField, Min(1)] private int defaultCapacity = 8;
    [Tooltip("Số object tối đa nằm CHỜ trong mỗi kho. Trả về khi kho đã đầy thì object bị Destroy.")]
    [SerializeField, Min(1)] private int maxSize = 64;

    private readonly Dictionary<GameObject, ObjectPool<PooledObject>> pools = new();
    private readonly HashSet<PooledObject> active = new();
    private readonly List<PooledObject> releaseBuffer = new();

    // ---------- API dùng ở mọi nơi ----------

    /// Thay cho Instantiate(prefab, position, rotation).
    public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        if (prefab == null) return null;
        if (Instance != null) return Instance.SpawnFromPool(prefab, position, rotation);

        // Scene thử không có PoolManager: chạy như Instantiate bình thường.
        T obj = Instantiate(prefab, position, rotation);
        foreach (IPoolable p in obj.GetComponentsInChildren<IPoolable>(true)) p.OnSpawned();
        return obj;
    }

    /// Thay cho Destroy(go). Object không đến từ pool (đặt tay trong scene) thì vẫn bị Destroy.
    public static void Despawn(GameObject go)
    {
        if (go == null) return;
        if (Instance != null && go.TryGetComponent(out PooledObject po) && po.Pool != null)
        {
            if (!po.IsInPool) po.Pool.Release(po);       // chặn trả 2 lần (vừa trúng Player vừa hết giờ)
            return;
        }
        Destroy(go);
    }

    /// Tạo sẵn cho kho có ít nhất `count` object, để lần bắn đầu tiên không phải Instantiate.
    public static void Prewarm<T>(T prefab, int count) where T : Component
    {
        if (prefab == null || Instance == null) return;
        ObjectPool<PooledObject> pool = Instance.GetPool(prefab.gameObject);
        if (pool.CountAll >= count) return;

        var temp = new List<PooledObject>(count);
        while (pool.CountAll < count) temp.Add(pool.Get());
        foreach (PooledObject po in temp) pool.Release(po);
    }

    // ---------- Bên trong ----------

    private T SpawnFromPool<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        PooledObject po = GetPool(prefab.gameObject).Get();
        po.transform.SetPositionAndRotation(position, rotation);   // đặt chỗ TRƯỚC khi bật
        po.gameObject.SetActive(true);
        po.NotifySpawned();                                         // rồi mới cho nó tự đặt lại trạng thái
        return po.GetComponent<T>();
    }

    private ObjectPool<PooledObject> GetPool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<PooledObject> pool)) return pool;

        ObjectPool<PooledObject> created = null;
        created = new ObjectPool<PooledObject>(
            createFunc: () => CreateInstance(prefab, created),
            actionOnGet: OnTakeFromPool,
            actionOnRelease: OnReturnToPool,
            actionOnDestroy: OnDestroyPooled,
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);

        pools.Add(prefab, created);
        return created;
    }

    private PooledObject CreateInstance(GameObject prefab, IObjectPool<PooledObject> pool)
    {
        GameObject go = Instantiate(prefab, transform);
        go.SetActive(false);
        go.name = prefab.name;                       // bỏ đuôi "(Clone)" cho Hierarchy dễ đọc
        PooledObject po = go.AddComponent<PooledObject>();
        po.Init(pool);
        return po;
    }

    private void OnTakeFromPool(PooledObject po)
    {
        po.IsInPool = false;
        active.Add(po);
        // Chưa bật ở đây: SpawnFromPool bật SAU khi đặt vị trí. Prewarm thì không bật.
    }

    private void OnReturnToPool(PooledObject po)
    {
        po.IsInPool = true;                          // đặt TRƯỚC NotifyDespawned để chặn gọi Despawn lồng nhau
        active.Remove(po);
        po.NotifyDespawned();
        po.gameObject.SetActive(false);
    }

    private void OnDestroyPooled(PooledObject po)
    {
        if (po != null) Destroy(po.gameObject);
    }

    // ---------- Đổi tầng ----------

    private void OnEnable() => SceneManager.sceneUnloaded += HandleSceneUnloaded;
    private void OnDisable() => SceneManager.sceneUnloaded -= HandleSceneUnloaded;

    /// Tầng cũ unload: đạn đang bay, đồ đang nằm trên sàn của tầng đó phải biến mất,
    /// giống như trước đây khi chúng nằm trong scene tầng và bị huỷ theo.
    private void HandleSceneUnloaded(Scene scene)
    {
        releaseBuffer.Clear();
        releaseBuffer.AddRange(active);              // không sửa HashSet trong lúc đang duyệt nó
        foreach (PooledObject po in releaseBuffer) Despawn(po.gameObject);
        releaseBuffer.Clear();
    }

    [ContextMenu("Module 9/In thống kê pool")]
    private void PrintStats()
    {
        foreach (var pair in pools)
            Debug.Log($"{pair.Key.name}: tổng {pair.Value.CountAll}, đang dùng {pair.Value.CountActive}, " +
                      $"trong kho {pair.Value.CountInactive}");
    }
}