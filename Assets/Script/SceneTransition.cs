using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance;
    public Image fadeImage;          // assign full screen black image
    public float fadeDuration = 0.6f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    void Start()
    {
        FadeIn();
    }
    public void FadeAndLoad(string sceneName)
    {
        fadeImage.raycastTarget = true; // block clicks during fade
        fadeImage.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            SceneManager.LoadScene(sceneName);
        });
    }

    public void FadeIn()
    {
        fadeImage.color = new Color(0, 0, 0, 1);
        fadeImage.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            fadeImage.raycastTarget = false;
        });
    }
}
