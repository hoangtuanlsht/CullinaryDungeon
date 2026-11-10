using UnityEngine;

/// Vệt sáng mờ ở miệng đường hầm chuyển phòng: alpha lên xuống nhẹ theo hình sin.
[RequireComponent(typeof(SpriteRenderer))]
public class GlowPulse : MonoBehaviour
{
    [SerializeField] private float minAlpha = 0.18f;
    [SerializeField] private float maxAlpha = 0.45f;
    [SerializeField] private float speed = 1.6f;      // rad/giây

    private SpriteRenderer sr;
    private float phase;

    public void SetRange(float min, float max) { minAlpha = min; maxAlpha = max; }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        phase = Random.value * Mathf.PI * 2f;          // các vệt không sáng cùng nhịp
    }

    private void Update()
    {
        var c = sr.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, 0.5f + 0.5f * Mathf.Sin(Time.time * speed + phase));
        sr.color = c;
    }
}
