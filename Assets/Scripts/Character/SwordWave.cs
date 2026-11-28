using UnityEngine;

/// [Module 12] Lưỡi sóng kiếm của Player. Lấy từ PoolManager, không Instantiate/Destroy.
/// Tách riêng khỏi Flame: Flame là đạn của quái (chỉ đánh tag Player), đạn này chỉ đánh tag Enemy.
/// Gộp hai phe vào một lớp thì một chỗ sửa nhầm là quái tự bắn trúng nhau hoặc Player tự trúng đạn mình.
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class SwordWave : MonoBehaviour, IPoolable
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float speed = 14f;                 // ô/giây
    [SerializeField] private float damage = 20f;
    [SerializeField, Min(0.1f)] private float lifetime = 0.6f;  // 14 × 0,6 ≈ 8 ô, gần một nửa khung camera
    [Tooltip("Chạm layer này thì biến mất (tường, sàn). Để Nothing = bay xuyên tường.")]
    [SerializeField] private LayerMask blockingLayers;
    [Tooltip("Hiệu ứng khi trúng (tùy chọn). Prefab có ParticleSystem + ParticleAutoDespawn.")]
    [SerializeField] private ParticleSystem hitVFX;

    private Cooldown life;     // struct của Module 6
    private bool alive;
    
    public float Damage => damage;

    private void Reset() => rb = GetComponent<Rigidbody2D>();
    private void Awake() { if (rb == null) rb = GetComponent<Rigidbody2D>(); }   // phòng khi quên kéo ô Rb trong Inspector

    public void OnSpawned()
    {
        alive = true;
        // Object lấy từ pool vẫn mang vị trí của lần bắn trước trong Rigidbody2D.
        // PoolManager chỉ đặt Transform; phải đồng bộ cả rb, nếu không Interpolate sẽ vẽ
        // sóng trượt từ chỗ cũ sang chỗ mới (trông như bay linh tinh).
        rb.position = transform.position;
        rb.rotation = transform.eulerAngles.z;
        rb.velocity = transform.right * speed;   // hướng do người bắn truyền vào qua rotation
        life.Start(lifetime);
    }

    public void OnDespawned()
    {
        alive = false;
        rb.velocity = Vector2.zero;
    }

    private void Update()
    {
        if (alive && life.IsReady) PoolManager.Despawn(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!alive) return;                                       // đã trúng trong frame này rồi

        if (other.CompareTag("Enemy"))
        {
            // Collider có thể nằm ở object con của quái, nên tìm Character ở cha.
            Character target = other.GetComponentInParent<Character>();
            if (target != null && !target.IsDead)
            {
                target.OnHit(damage, transform);
                Explode();
            }
        }
        else if ((blockingLayers.value & (1 << other.gameObject.layer)) != 0)
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
