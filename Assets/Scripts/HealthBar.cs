using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image imageFill;
    float hp;
    float maxHp;
    void Update()
    {
        imageFill.fillAmount = Mathf.Lerp(imageFill.fillAmount,hp / maxHp,Time.deltaTime * 5f);
    }
    public void OnInit(float maxHp)
    {
        this.maxHp = maxHp;
        hp = maxHp;
        imageFill.fillAmount = 1f;
    }
    public void SetNewHP(float hp)
    {
        this.hp = hp;
        imageFill.fillAmount = hp / maxHp;
    }
}
