using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 6] Bộ đếm hồi chiêu — không cần ai gọi Tick mỗi frame.
public struct Cooldown
{
    private float readyAt;
    private float duration;
    public bool IsReady => Time.time >= readyAt;
    public float Remaining => Mathf.Max(0f, readyAt - Time.time);   // để vẽ icon hồi chiêu sau này
    public float Progress => duration <= 0f ? 1f : 1f - Mathf.Clamp01(Remaining / duration);   // 0..1, để vẽ thanh hồi chiêu 
    public void Start(float duration)
    {
        this.duration = duration;
        readyAt = Time.time + duration;
    }
    public void Reset() => readyAt = 0f;
}
