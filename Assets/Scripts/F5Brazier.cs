using UnityEngine;

/// [Tầng 5] Đèn thờ trong mê cung. Player chạm vào thì thắp: bật ngọn lửa và các vòng "reveal"
/// (vẽ lên hai lớp fog of war), nên vùng quanh đèn luôn sáng và làm mốc định hướng trong bóng tối.
/// Trạng thái thắp chưa được lưu: vào lại scene thì đèn tắt (cùng lúc với "bộ nhớ" fog bị xoá). Muốn lưu, thêm cờ theo ID vào WorldProgress.
[RequireComponent(typeof(Collider2D))]
public class F5Brazier : MonoBehaviour
{
    [SerializeField] private GameObject litVisual;
    [SerializeField] private GameObject unlitVisual;
    [Tooltip("Các vòng sáng đặt trên layer FogOfWar / FogOfWarLayer2.")]
    [SerializeField] private GameObject[] reveal;
    [SerializeField] private bool litAtStart;

    public bool IsLit { get; private set; }

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        SetLit(litAtStart);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsLit && other.CompareTag("Player")) SetLit(true);
    }

    public void SetLit(bool on)
    {
        IsLit = on;
        if (litVisual) litVisual.SetActive(on);
        if (unlitVisual) unlitVisual.SetActive(!on);
        if (reveal != null) foreach (var r in reveal) if (r) r.SetActive(on);
    }
}
