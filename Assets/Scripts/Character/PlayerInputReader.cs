using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 6] Chỗ DUY NHẤT đọc input gameplay. Các class khác hỏi "người chơi muốn gì",
/// không hỏi "phím nào đang bấm".
public class PlayerInputReader : MonoBehaviour
{
    private PlayerControls controls;
    private const float StickThreshold = 0.3f;

    private void Awake() => controls = new PlayerControls();
    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();
    private void OnDestroy() => controls.Dispose();

    // --- Trạng thái đang giữ ---
    /// -1, 0 hoặc 1 — đúng như Input.GetAxisRaw("Horizontal").
    public float MoveX => Snap(controls.Gameplay.Move.ReadValue<Vector2>().x);
    public float MoveY => Snap(controls.Gameplay.Move.ReadValue<Vector2>().y);   // để dành Module 7
    public bool RunHeld => controls.Gameplay.Run.IsPressed();
    public bool DefendHeld => controls.Gameplay.Defend.IsPressed();

    // --- Sự kiện 1 frame (GetKeyDown / GetKeyUp) — chỉ đọc trong Update ---
    public bool JumpPressed => controls.Gameplay.Jump.WasPressedThisFrame();
    public bool JumpReleased => controls.Gameplay.Jump.WasReleasedThisFrame();
    public bool AttackPressed => controls.Gameplay.Attack.WasPressedThisFrame();
    public bool InteractPressed => controls.Gameplay.Interact.WasPressedThisFrame();
    public bool CraftPressed => controls.Gameplay.Craft.WasPressedThisFrame();
    public bool InventoryPressed => controls.Gameplay.Inventory.WasPressedThisFrame();

    public void SetGameplayEnabled(bool enabled)
    {
        if (enabled) controls.Gameplay.Enable();
        else controls.Gameplay.Disable();
    }

    private static float Snap(float v) => Mathf.Abs(v) < StickThreshold ? 0f : Mathf.Sign(v);
}

