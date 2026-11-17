using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ManaBar : MonoBehaviour
{
    [SerializeField] private Image imageFill;
    [SerializeField] private Player player;
    private Coroutine flashManaRountine;
    private Color originalColor;
    [SerializeField] private Color flashColor = new Color(1f, 0.55f, 0.55f);   // hồng đỏ nhạt

    private void Awake()
    {
        originalColor = imageFill.color;
    }

    private void OnEnable()
    {
        player.PlayerMana.OnManaLevelChanged += HandleManaLevelChanged;
        player.PlayerMana.OnManaNotEnough += HandleManaNotEnough;
        HandleManaLevelChanged(player.PlayerMana.ManaLevel, player.PlayerMana.ManaMaxLevel);
    }
    private void OnDisable()
    {
        player.PlayerMana.OnManaLevelChanged -= HandleManaLevelChanged;
        player.PlayerMana.OnManaNotEnough -= HandleManaNotEnough;
        imageFill.color = originalColor;
    }
    public void HandleManaLevelChanged(float current, float max)
    {
        imageFill.fillAmount = current / max;
    }
    public void HandleManaNotEnough()
    {
        Debug.Log($"ManaBar nhận thiếu mana, đổi màu {imageFill.name}", imageFill);
        Stop(ref flashManaRountine);
        flashManaRountine = StartCoroutine(FlashManaBar());

    }
    public IEnumerator FlashManaBar()
    {
        imageFill.color = flashColor;
        yield return new WaitForSecondsRealtime(0.2f);
        imageFill.color = originalColor;
    }
    private void Stop(ref Coroutine routine)
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
    }
}
