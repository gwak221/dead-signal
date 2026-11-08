using UnityEngine;
[ExecuteAlways]
[RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
public class DepthTestedText : MonoBehaviour
{
    public Shader depthShader;
    Material material;
    TextMesh label;
    void OnEnable()
    {
        label = GetComponent<TextMesh>();
        if (depthShader == null) depthShader = Shader.Find("DeadSignal/DepthTestedText");
        if (depthShader == null) return;
        material = new Material(depthShader) { hideFlags = HideFlags.HideAndDontSave };
        GetComponent<MeshRenderer>().sharedMaterial = material;
        Font.textureRebuilt += OnFontRebuilt;
        RefreshAtlas();
    }
    void OnFontRebuilt(Font font) { if (label != null && label.font == font) RefreshAtlas(); }
    void LateUpdate() { RefreshAtlas(); }
    void RefreshAtlas()
    {
        if (material == null || label == null || label.font == null) return;
        material.mainTexture = label.font.material.mainTexture;
    }
    void OnDisable()
    {
        Font.textureRebuilt -= OnFontRebuilt;
        if (material != null)
        {
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
