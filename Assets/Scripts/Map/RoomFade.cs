using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// Màn đen khi đổi phòng (kiểu Hollow Knight).
/// RoomTracker đổi phòng ở FixedUpdate → cùng frame đó: phủ đen ngay + ép Brain chuyển Cut (không trượt camera),
/// giữ đen tới khi camera mới đã đứng yên, rồi mở dần. Không khóa điều khiển.
/// Bỏ qua lần đổi phòng đầu tiên sau khi load scene / hồi sinh / qua SceneDoor (phòng trước là null hoặc đã bị unload).
public class RoomFade : MonoBehaviour
{
    [SerializeField] private float hold = 0.1f;          // giữ đen tối thiểu (giây, không phụ thuộc timeScale)
    [SerializeField] private float fadeOut = 0.2f;
    [SerializeField] private float maxWait = 0.5f;        // chờ Brain hết blend tối đa
    [SerializeField] private int fallbackSortOrder = 500; // dưới ScreenFader (đổi scene) nếu tìm thấy nó

    private RoomTracker tracker;
    private Room last;
    private CanvasGroup group; 
    private Coroutine running;

    private void Awake()
    {
        tracker = GetComponent<RoomTracker>();
        BuildOverlay();
    }

    private void OnEnable() { if (tracker != null) tracker.OnRoomChanged += HandleRoomChanged; }
    private void OnDisable() { if (tracker != null) tracker.OnRoomChanged -= HandleRoomChanged; }

    private void HandleRoomChanged(Room room)
    {
        bool hadPrevious = last != null;   // Room của scene đã unload == null (null giả của Unity)
        last = room;
        if (!hadPrevious) return;

        group.alpha = 1f;                  // đen ngay trong frame này, trước khi camera kịp render cảnh trượt
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Transition());
    }

    private IEnumerator Transition()
    {
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            var savedBlend = brain.m_DefaultBlend;
            var savedCustom = brain.m_CustomBlends;
            brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
            brain.m_CustomBlends = null;
            yield return null;             // Brain xử lý vcam mới ở LateUpdate của frame đổi phòng
            yield return null;
            brain.m_DefaultBlend = savedBlend;
            brain.m_CustomBlends = savedCustom;
        }

        float t = 0f;
        while (t < hold || (brain != null && brain.IsBlending && t < maxWait))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            yield return null;
        }
        group.alpha = 0f;
        running = null;
    }

    private void BuildOverlay()
    {
        var go = new GameObject("RoomFadeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var fader = FindObjectOfType<ScreenFader>(true);
        var fc = fader != null ? fader.GetComponentInParent<Canvas>() : null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = fc != null ? fc.sortingOrder - 1 : fallbackSortOrder;

        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var image = img.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }
}
