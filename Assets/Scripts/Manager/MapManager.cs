using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapManager : MonoBehaviour, Interact
{
    public CinemachineVirtualCamera map1Cam;
    public CinemachineVirtualCamera map2Cam;

    [Header("Loading UI")]
    [SerializeField] private Image loadingBar;
    [SerializeField] private GameObject loadingBarObject;
    public float loadingDuration = 1.5f; // Thời gian chạy thanh loading (giây)

    [Header("Teleport Settings")]
    public GameObject teleport;
    public GameObject player;
    public bool isNextMap = false;

    private void Awake()
    {
        loadingBarObject.SetActive(false);
    }
    public void InteractWithObject()
    {
        StartCoroutine(TeleportWithLoading());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            player = other.gameObject;

            if (isNextMap)
            {
                
                StartCoroutine(TeleportWithLoading());
            }
        }
    }

    private IEnumerator TeleportWithLoading()
    {
        // 1. Bật UI Loading và reset thanh trạng thái về 0
        if (loadingBarObject != null)
            loadingBarObject.SetActive(true);

        if (loadingBar != null)
            loadingBar.fillAmount = 0f;

        if (player != null && teleport != null)
        {
            player.transform.position = teleport.transform.position;
        }
        if (map1Cam != null && map2Cam != null)
        {
            map1Cam.Priority = 0;
            map2Cam.Priority = 10;
        }
        float timer = 0f;
        while (timer < loadingDuration)
        {
            timer += Time.deltaTime;
            if (loadingBar != null)
            {
                loadingBar.fillAmount = timer / loadingDuration;
            }
            yield return null; // Đợi frame tiếp theo
        }

        // Đảm bảo thanh loading đầy 100%
        if (loadingBar != null)
            loadingBar.fillAmount = 1f;
        
        yield return new WaitForSeconds(0.2f);

        // 5. Tắt UI Loading
        if (loadingBarObject != null)
            loadingBarObject.SetActive(false);
    }
}
