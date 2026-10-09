using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image imageFill;
    [SerializeField] Vector3 offset;

    float hp;
    float maxHp;
    private Transform target;

    private Character owner;


    void Update()
    {
        imageFill.fillAmount = Mathf.Lerp(imageFill.fillAmount,hp / maxHp,Time.deltaTime * 2f);
        transform.position = target.position + offset;
    }
    public void OnInit(float maxHp, Transform target)
    {
        this.target = target;
        this.maxHp = maxHp;
        hp = maxHp;
        imageFill.fillAmount = 1f;
    }
    //public void SetNewHP(float hp)
    //{
    //    this.hp = hp;
    //    imageFill.fillAmount = hp / maxHp;
    //}
    public void Bind(Character newOwner)
    {
        if (owner != null) owner.OnHealthChanged -= HandleHealthChanged;
        owner = newOwner;
        target = newOwner.transform;
        owner.OnHealthChanged += HandleHealthChanged;
    }
    private void HandleHealthChanged(float current, float max)
    {
        maxHp = max; hp = current;
        imageFill.fillAmount = hp / maxHp;
    }
    private void OnDestroy()
    {
        if (owner != null) owner.OnHealthChanged -= HandleHealthChanged;
    }
}
