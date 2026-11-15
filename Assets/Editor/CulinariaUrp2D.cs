using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Bật / tắt URP với 2D Renderer cho cả project (cần cho Light2D — ánh sáng thật từ đuốc, dung nham).
/// - "Bật": tạo Assets/Settings/URP/Culinaria_Renderer2D.asset + Culinaria_URP.asset (nếu chưa có) rồi gán vào Graphics và mọi mức Quality.
/// - "Tắt": gỡ pipeline khỏi Graphics và Quality → game quay về Built-in đúng như trước. Asset URP vẫn nằm đó, không sao.
/// Sprite dùng material mặc định (Sprites-Default) KHÔNG bị ánh sáng tác động, nên các tầng chưa đặt đèn trông như cũ.
/// Chỉ những gì được gán material Sprite-Lit-Default (hiện là tầng 6) mới sáng/tối theo đèn.
public static class CulinariaUrp2D
{
    const string Dir = "Assets/Settings/URP";
    const string RendererPath = Dir + "/Culinaria_Renderer2D.asset";
    const string UrpPath = Dir + "/Culinaria_URP.asset";
    const string UrpPackage = "Packages/com.unity.render-pipelines.universal";

    public static bool IsActive => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

    [MenuItem("Tools/Culinaria/Lighting/Bat URP 2D (anh sang that)")]
    public static void Enable()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Settings", "URP");

        var rd = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
        if (rd == null)
        {
            rd = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(rd, RendererPath);
        }
        // Nạp shader/material mặc định của 2D Renderer (việc mà menu Create của Unity vẫn làm)
        var reloader = System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEngine.Rendering.ResourceReloader")).FirstOrDefault(t => t != null);
        var m = reloader?.GetMethods().FirstOrDefault(x => x.Name == "ReloadAllNullIn" && x.GetParameters().Length == 2);
        if (m != null) m.Invoke(null, new object[] { rd, UrpPackage });
        EditorUtility.SetDirty(rd);

        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
        if (urp == null)
        {
            urp = UniversalRenderPipelineAsset.Create(rd);
            AssetDatabase.CreateAsset(urp, UrpPath);
        }
        GraphicsSettings.defaultRenderPipeline = urp;
        int cur = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = urp; }
        QualitySettings.SetQualityLevel(cur, false);
        AssetDatabase.SaveAssets();

        // Kiểm tra renderer đã có đủ shader chưa
        var so = new SerializedObject(rd); int nullRefs = 0, refs = 0;
        var it = so.GetIterator();
        while (it.NextVisible(true))
            if (it.propertyType == SerializedPropertyType.ObjectReference) { refs++; if (it.objectReferenceValue == null) nullRefs++; }
        Debug.Log($"[URP2D] BẬT. Pipeline hiện tại: {GraphicsSettings.currentRenderPipeline?.name}. ResourceReloader: {(m != null ? "có" : "KHÔNG THẤY")}. " +
                  $"Renderer2D: {refs} tham chiếu, {nullRefs} trống, {rd.lightBlendStyles.Length} blend style. Quality: {QualitySettings.names.Length} mức.");
    }

    [MenuItem("Tools/Culinaria/Lighting/Tat URP (ve Built-in)")]
    public static void Disable()
    {
        GraphicsSettings.defaultRenderPipeline = null;
        int cur = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = null; }
        QualitySettings.SetQualityLevel(cur, false);
        AssetDatabase.SaveAssets();
        Debug.Log("[URP2D] TẮT. Pipeline hiện tại: " + (GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name));
    }
}
