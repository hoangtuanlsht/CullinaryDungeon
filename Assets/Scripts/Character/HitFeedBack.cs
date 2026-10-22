using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Hiệu ứng phản hồi khi bị đánh trúng. Gắn vào prefab quái nào cần.
public class HitFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Material flashMaterial;
    [SerializeField] private float flashDuration = 0.08f;

    private Material originalMaterial;
    private Coroutine flashRoutine;

    private void Awake()
    {
        // TODO 1: nếu spriteRenderer chưa được kéo vào Inspector thì tự tìm
        //         (gợi ý: sprite của quái có thể nằm ở object con)
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        // TODO 2: lưu lại material gốc để sau này trả về
        originalMaterial = spriteRenderer.material;
    }

    public void Play()
    {
        // TODO 3: nếu đang có flash chạy dở thì dừng nó lại
        if(flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // TODO 4: đổi sang material nháy trắng
        spriteRenderer.material = flashMaterial;
        // TODO 5: chờ flashDuration (WaitForSeconds hay Realtime? xem mục 5)
        yield return new WaitForSeconds(flashDuration);
        // TODO 6: trả material gốc về
        spriteRenderer.material = originalMaterial;
        flashRoutine = null;
    }

    private void OnDisable()
    {
        // TODO 7: nếu đang flash dở thì trả material gốc về
        if(spriteRenderer.material != originalMaterial) spriteRenderer.material = originalMaterial;

    }
}
