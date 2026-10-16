using UnityEngine;

/// [Module 9] Đạn lửa của Devil. Lấy từ PoolManager, không Instantiate/Destroy.
public class Flame : MonoBehaviour, IPoolable
{
    public ParticleSystem hitVFX;
    public Rigidbody2D rb;
    [SerializeField] private float speed;
    [SerializeField] private float damage;
    [SerializeField, Min(0.1f)] private float lifetime = 5f;
    [Tooltip("Chạm layer này thì nổ (tường, sàn). Để Nothing = bay xuyên tường như cũ.")]
    [SerializeField] private LayerMask blockingLayers;

    private Cooldown life;          // struct của Module 6
    private bool alive;

    public void OnSpawned()
    {
        alive = true;
        rb.velocity = transform.right * speed;
        life.Start(lifetime);
    }

    public void OnDespawned()
    {
        alive = false;
    }

    private void Update()
    {
        if (alive && life.IsReady) PoolManager.Despawn(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!alive) return;                                         // đã nổ trong frame này rồi

        if (collision.CompareTag("Player"))
        {
            collision.GetComponent<Character>().OnHit(damage, transform);
            Explode();
        }
        else if ((blockingLayers.value & (1 << collision.gameObject.layer)) != 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        alive = false;
        if (hitVFX != null) PoolManager.Spawn(hitVFX, transform.position, transform.rotation);
        PoolManager.Despawn(gameObject);
    }
}