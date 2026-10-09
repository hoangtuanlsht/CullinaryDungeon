using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    public IEnumerator FadeTo(float target, float duration)
    {
        group.blocksRaycasts = true;                 // chặn click UI khi đang chuyển
        float start = group.alpha, t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;             // vẫn chạy nếu game đang pause (timeScale = 0)
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        group.alpha = target;
        group.blocksRaycasts = target > 0.5f;
    }
}
