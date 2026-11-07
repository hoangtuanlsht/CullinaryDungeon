using UnityEngine;

/// [Tầng 5] Xoá "bộ nhớ" fog of war (render texture của camera không tự xoá) mỗi lần scene bắt đầu,
/// để mê cung luôn tối hoàn toàn khi người chơi mới vào. Render texture là asset nên nếu không xoá
/// nó sẽ giữ lại vệt đã đi từ lần chơi trước (kể cả trong Editor).
public class F5FogRig : MonoBehaviour
{
    [SerializeField] private RenderTexture explored;

    private void Awake()
    {
        if (explored == null) return;
        var prev = RenderTexture.active;
        RenderTexture.active = explored;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }
}
