using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillCoolDownBar : MonoBehaviour
{
    [SerializeField] private Image skillCanvas;
    [SerializeField] private Image imageFill;
    [SerializeField] private TextMeshProUGUI textRemaining;

    [SerializeField]private Player player;
    private bool isSkillUnlock = false;
    private Color originalColor;
    private int lastTenths = -1;
    private void Awake()
    {
        originalColor = skillCanvas.color;
    }
    private void Start()
    {
        Refresh();
    }
    private void Update()
    {
        if (isSkillUnlock)
        {
            SkillCoolDownChanged(player.Combat.SwordWaveRemaining,player.Combat.SwordWaveProgress);
        }
        
    }
    private void OnEnable()
    {
        player.Abilities.OnUnlocked += HandleSkillUnlocked;           
        player.PlayerMana.OnManaLevelChanged += HandleManaLevelChanged;
    }
    private void OnDisable()
    {
        player.Abilities.OnUnlocked -= HandleSkillUnlocked;
        player.PlayerMana.OnManaLevelChanged -= HandleManaLevelChanged;
    }
    private void HandleSkillUnlocked(Ability skill)
    {
        Refresh();
    }
    private void HandleManaLevelChanged(float current,float max)
    {
        Refresh();
    }
    public void SkillCoolDownChanged(float remaining,float progress)
    {
        imageFill.fillAmount = progress;
        if(remaining == 0) { textRemaining.gameObject.SetActive(false); lastTenths = -1; return; }
        else
        {
            int tenths = Mathf.CeilToInt(remaining * 10f);
            if(tenths != lastTenths)
            {
                lastTenths = tenths;
                textRemaining.gameObject.SetActive(true);
                textRemaining.text = tenths == 0 ? "" : (tenths / 10f).ToString("0.0");
            }
        }
    }
    private void Refresh()
    {
       
        if (player.Abilities.Has(Ability.SwordWave))
        {
            skillCanvas.gameObject.SetActive(true);
            isSkillUnlock = true;
            if(player.PlayerMana.ManaLevel < player.Combat.SwordWaveCost)
            {
                skillCanvas.color = Color.red;
                Color color = skillCanvas.color;
                color.a = 1f;
                skillCanvas.color = color;
            }
            else
            {
                skillCanvas.color = originalColor;
                Color color = skillCanvas.color;
                color.a = 0f;
                skillCanvas.color = color;
            }
        }
        else
        {
            skillCanvas.gameObject.SetActive(false);
            isSkillUnlock = false;
        }
    }
}
