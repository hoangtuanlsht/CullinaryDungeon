using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveIndicator : MonoBehaviour
{
    [SerializeField] private GameObject icon;
    [SerializeField] private float showTime = 1f;
    private Coroutine routine;

    private void Start()
    {
        icon.SetActive(false);
        if (SaveManager.Instance != null) SaveManager.Instance.OnSaved += Show;
    }
    private void OnDestroy()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.OnSaved -= Show;
    }
    private void Show()
    {
        if(routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine());
    }
    private IEnumerator ShowRoutine()
    {
        icon.SetActive(true);
        yield return new WaitForSecondsRealtime(showTime);
        icon.SetActive(false);
        routine = null;
    }
}
