using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;
//using Cinemachine;

public class CheckPoint : MonoBehaviour,Interact
{
    [Tooltip("Duy nhất toàn game, vd F2_CP01. Không đổi sau khi đã có save.")]
    [SerializeField] private string checkpointId;
    private Player player;
    public CinemachineVirtualCamera areaCamera;
    public string CheckPointId => checkpointId;
    private void Awake()
    {
        if (string.IsNullOrEmpty(checkpointId)) Debug.LogError($"{name}:Chưa đặt CheckPoint Id", this);
    }
    public void InteractWithObject()
    {
        if (player == null) return;
        // Lấy tên camera nếu areaCamera đã được gán
        string camName = areaCamera != null ? areaCamera.name : "";
        player.PlayerMana.AddMana(player.PlayerMana.ManaMaxLevel); // hồi full mana khi save
        // Cập nhật UpdateSavePoint để truyền thêm tên camera
        player.UpdateSavePoint(transform.position, camName, checkpointId, gameObject.scene.name);
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        player = collision.GetComponent<Player>();
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player")) player = null;
    }
}
