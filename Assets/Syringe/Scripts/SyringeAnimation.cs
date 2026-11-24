using UnityEngine;
using System.Collections;
public class SyringeAnimation : MonoBehaviour {
    public Transform plunger, piston, liquid;
    [Min(0.01f)] public float duration = 0.65f;
    public const float Stroke = 0.055f;
    public const float LiquidBase = 0.042f;
    [Range(0,1)] public float progress;
    Coroutine motion;
    void Awake() { SetProgress(progress); }
    public void SetProgress(float value) {
        progress = Mathf.Clamp01(value);
        float d = Stroke * progress;
        if(plunger) plunger.localPosition = Vector3.down*d;
        if(piston) piston.localPosition = Vector3.down*d;
        if(liquid) {
            float s = 1-progress;
            liquid.localScale = new Vector3(1, Mathf.Max(0.0001f,s),1);
            liquid.localPosition = Vector3.up*(LiquidBase*(1-Mathf.Max(0.0001f,s)));
            liquid.gameObject.SetActive(s>0.0001f);
        }
    }
    public void Inject() { AnimateTo(1); }
    public void ResetSyringe() { AnimateTo(0); }
    public void AnimateTo(float target) {
        if(motion != null) StopCoroutine(motion);
        motion = StartCoroutine(Travel(Mathf.Clamp01(target)));
    }
    IEnumerator Travel(float target) {
        float start = progress, t=0;
        while(t<1) { t += Time.deltaTime/Mathf.Max(.01f,duration); SetProgress(Mathf.Lerp(start,target,Mathf.SmoothStep(0,1,t))); yield return null; }
        SetProgress(target); motion=null;
    }
}
