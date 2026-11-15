using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Làm đèn 2D (đuốc, lửa trại, dung nham) nhấp nháy nhẹ quanh cường độ gốc.
[RequireComponent(typeof(Light2D))]
public class LightFlicker2D : MonoBehaviour
{
    [SerializeField] float amplitude = 0.18f;   // 0.18 = dao động ±18%
    [SerializeField] float speed = 7f;

    Light2D light2D;
    float baseIntensity, seed;

    void Awake()
    {
        light2D = GetComponent<Light2D>();
        baseIntensity = light2D.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
        light2D.intensity = baseIntensity * (1f + 2f * amplitude * n);
    }
}
