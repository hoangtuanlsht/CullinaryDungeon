using UnityEngine;

/// Chạy vòng các khung sprite (đuốc, đèn tường, tinh thể...). Không cần Animator Controller.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 8f;
    [Tooltip("Lệch pha ngẫu nhiên để các đuốc không cháy đồng loạt")]
    [SerializeField] private bool randomPhase = true;

    private SpriteRenderer sr;
    private float phase;

    public void Setup(Sprite[] newFrames, float newFps)
    {
        frames = newFrames;
        fps = newFps;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (randomPhase) phase = Random.value * 10f;
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;
        int i = (int)((Time.time + phase) * fps) % frames.Length;
        sr.sprite = frames[i];
    }
}
