using System.Collections;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.UI.ProceduralImage;

public class Outline : MonoBehaviour
{
  [SerializeField]  ProceduralImage outlineImage;
    public float fadeDuration;
    public float max;
    void Start()
    {
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        while (true)
        {
            float alpha = Mathf.PingPong(Time.time * fadeDuration, max); // oscillates between 0 and 1
            Color color = outlineImage.color;
            color.a = alpha;
            outlineImage.color = color;
            yield return null;
        }
    }
}
