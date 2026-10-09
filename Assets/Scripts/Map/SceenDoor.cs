using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ExitDirection { Left, Right, Up, Down }

/// [Module 5] Cửa nối sang scene khác. Trỏ tới "cửa targetDoorId của targetScene", không trỏ tọa độ.
[RequireComponent(typeof(Collider2D))]
public class SceneDoor : MonoBehaviour, Interact
{
    [Header("Cửa này")]
    [SerializeField] private string doorId;                  // duy nhất TRONG scene, vd "F1_Down"
    [Tooltip("Chỗ Player xuất hiện khi đi VÀO scene qua cửa này. Phải nằm NGOÀI trigger của cửa.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Hướng Player di chuyển khi bước ra từ cửa này vào phòng.")]
    [SerializeField] private ExitDirection enterDirection = ExitDirection.Right;

    [Header("Dẫn tới")]
    [SerializeField] private SceneField targetScene;
    [SerializeField] private string targetDoorId;

    [Header("Kích hoạt")]
    [Tooltip("true: phải bấm E (cửa Guild, thang máy). false: chạm là đi (lối ở mép phòng).")]
    [SerializeField] private bool requireInteract = false;

    public string DoorId => doorId;
    public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;
    public ExitDirection EnterDirection => enterDirection;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!requireInteract && other.CompareTag("Player")) Use();
    }

    public void InteractWithObject()
    {
        if (requireInteract) Use();
    }

    private void Use()
    {
        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogError("Chưa có SceneTransitionManager trong PersistentGameplay", this);
            return;
        }
        SceneTransitionManager.Instance.TravelTo(targetScene, targetDoorId, gameObject.scene);
    }
}
