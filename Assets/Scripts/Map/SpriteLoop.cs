using UnityEngine;

/// Lặp các khung hình sprite (thác nước, đuốc...). Giữ nguyên drawMode/size của SpriteRenderer
/// nên dùng được với SpriteRenderer kiểu Tiled (thác nước dài nhiều ô).
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteLoop : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 10f;
    [SerializeField] private float timeOffset;     // lệch pha để các thác cạnh nhau không chạy y hệt

    private SpriteRenderer sr;
    private int last = -1;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;
        int i = (int)((Time.time + timeOffset) * fps) % frames.Length;
        if (i == last) return;
        last = i;
        sr.sprite = frames[i];
    }
}
