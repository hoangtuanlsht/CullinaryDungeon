using UnityEngine;

/// [Tầng 5] Lặp một dãy sprite (đuốc, lửa). Không cần Animator cho vật trang trí đơn giản.
[RequireComponent(typeof(SpriteRenderer))]
public class F5SpriteLoop : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.1f)] private float fps = 8f;
    [Tooltip("Lệch pha 0..1 để các ngọn đuốc không nhấp nháy cùng lúc.")]
    [SerializeField, Range(0f, 1f)] private float phase;

    private SpriteRenderer sr;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;
        int i = (int)((Time.time * fps) + phase * frames.Length) % frames.Length;
        sr.sprite = frames[i];
    }
}
