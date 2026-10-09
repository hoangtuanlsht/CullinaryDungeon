using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 6] Bộ đếm hồi chiêu — không cần ai gọi Tick mỗi frame.
public struct Cooldown
{
    private float readyAt;
    public bool IsReady => Time.time >= readyAt;
    public float Remaining => Mathf.Max(0f, readyAt - Time.time);   // để vẽ icon hồi chiêu sau này
    public void Start(float duration) => readyAt = Time.time + duration;
    public void Reset() => readyAt = 0f;
}
