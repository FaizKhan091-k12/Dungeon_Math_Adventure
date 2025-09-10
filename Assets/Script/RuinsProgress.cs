using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// If you use ProceduralImage package, include its namespace:
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UI.ProceduralImage;
#endif

[DisallowMultipleComponent]
public class RuinsProgress : MonoBehaviour
{
    [Header("Runes (bottom -> top)")]
    [Tooltip("Assign the rune images in the order you want them filled: bottom first, top last.")]
    public List<Graphic> runes = new List<Graphic>(); // Image or ProceduralImage (Graphic is base)

    [Header("Settings")]
    public int maxRunesToFill = 8;         // usually equal to runes.Count
    public float fillTweenTime = 0.35f;    // time to tween 0->1 or 1->0
    public Ease fillEase = Ease.OutCubic;  // tween easing

    // internal
    int filledCount = 0; // how many runes fully filled (0..maxRunesToFill)


    void Start()
    {
        // Optional validation
        if (maxRunesToFill <= 0) maxRunesToFill = Mathf.Max(1, runes.Count);
        // initialize all to 0
        for (int i = 0; i < runes.Count; i++)
        {
            SetFillInstant(runes[i], 0f);
        }
        filledCount = 0;
    }

    // PUBLIC API — call this on correct answer
    public void OnCorrectAnswer()
    {
        if (filledCount >= maxRunesToFill) return;

        // pick the next rune index (bottom->top)
        int idx = filledCount;
        if (idx < 0 || idx >= runes.Count) return;

        // animate fill 0 -> 1
        AnimateSetFill(runes[idx], 1f, fillTweenTime, fillEase);
        filledCount++;

        // Optionally do something when fully complete
        if (filledCount >= maxRunesToFill)
        {
            OnAllRunesFilled();
        }
    }

    // PUBLIC API — call this on wrong answer
    // Behavior: if there's at least one filled rune, unfill the last filled rune (topmost filled)
    public void OnWrongAnswer()
    {
        if (filledCount <= 0) return;

        // last filled index is filledCount - 1
        int idx = filledCount - 1;
        if (idx < 0 || idx >= runes.Count) return;

        // animate fill 1 -> 0
        AnimateSetFill(runes[idx], 0f, fillTweenTime, fillEase);
        filledCount--;
    }

    // instant reset (all runes -> 0)
    public void ResetAll()
    {
        for (int i = 0; i < runes.Count; i++)
            SetFillInstant(runes[i], 0f);
        filledCount = 0;
    }

    // called when last rune filled
    void OnAllRunesFilled()
    {
        Debug.Log("[RuinsProgress] All runes lit!");
    
        // Optionally call your level-complete logic or event here.
    }

    // Helper: animate fill for a Graphic that is either Image or ProceduralImage
    void AnimateSetFill(Graphic g, float targetFill, float time, Ease ease)
    {
        if (g == null) return;

        // kill existing tweens on this object (safety)
        DOTween.Kill(g);

        // 1) Standard UnityEngine.UI.Image
        Image uiImg = g as Image;
        if (uiImg != null)
        {
            // ensure type is Filled (works best)
            // NOTE: If you're using sprite types other than Filled, fillAmount may not work visually.
            uiImg.type = Image.Type.Filled;
            uiImg.fillAmount = Mathf.Clamp01(uiImg.fillAmount); // normalize
            uiImg.DOFillAmount(Mathf.Clamp01(targetFill), time).SetEase(ease);
            return;
        }

#if UNITY_2019_1_OR_NEWER
        // 2) ProceduralImage (from Unity's package)
        ProceduralImage proc = g as ProceduralImage;
        if (proc != null)
        {
            // ProceduralImage exposes fillAmount similar to Image
            // some versions use property 'fillAmount' (float)
            float current = proc.fillAmount;
            DOTween.To(() => current, x => { current = x; proc.fillAmount = x; }, Mathf.Clamp01(targetFill), time).SetEase(ease);
            return;
        }
#endif

        // 3) Generic fallback: try to find a component that has 'fillAmount' property via reflection
        var comp = g.GetComponent<Behaviour>();
        if (comp != null)
        {
            var type = comp.GetType();
            var prop = type.GetProperty("fillAmount");
            if (prop != null && prop.PropertyType == typeof(float))
            {
                float current = (float)prop.GetValue(comp, null);
                DOTween.To(() => current, x => { current = x; prop.SetValue(comp, x, null); }, Mathf.Clamp01(targetFill), time).SetEase(ease);
                return;
            }
        }

        // If we get here, we couldn't find a fill property. As a fallback, animate alpha to simulate fill.
        // (Not ideal but safe.)
        float startA = g.color.a;
        Color startColor = g.color;
        DOTween.ToAlpha(() => startColor, c => { g.color = c; startColor = c; }, targetFill > 0.5f ? 1f : 0.2f, time).SetEase(ease);
    }

    // instant set (no tween)
    void SetFillInstant(Graphic g, float value)
    {
        if (g == null) return;

        Image uiImg = g as Image;
        if (uiImg != null)
        {
            uiImg.type = Image.Type.Filled;
            uiImg.fillAmount = Mathf.Clamp01(value);
            return;
        }

#if UNITY_2019_1_OR_NEWER
        ProceduralImage proc = g as ProceduralImage;
        if (proc != null)
        {
            proc.fillAmount = Mathf.Clamp01(value);
            return;
        }
#endif

        // reflection fallback
        var comp = g.GetComponent<Behaviour>();
        if (comp != null)
        {
            var type = comp.GetType();
            var prop = type.GetProperty("fillAmount");
            if (prop != null && prop.PropertyType == typeof(float))
            {
                prop.SetValue(comp, Mathf.Clamp01(value), null);
                return;
            }
        }

        // fallback to alpha
        Color c = g.color;
        c.a = Mathf.Clamp01(value);
        g.color = c;
    }

    // Optional: utility to get current progress [0..1]
    public float GetOverallProgressNormalized()
    {
        if (maxRunesToFill <= 0) return 0f;
        return (float)filledCount / (float)maxRunesToFill;
    }
}
