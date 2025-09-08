using System.Collections;

using UnityEngine;

using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

public class MainMenuController : MonoBehaviour
{
        [Header("Debug Menu")]

    [SerializeField] bool isMainMenu;
    public WizardDialogue wizardDialogue;


    [Header("UI Elements")]
    [SerializeField] ProceduralImage transitionImage;
    [SerializeField] ProceduralImage chapter1Icon;
    [SerializeField] ParticleSystem[] particles;
    [SerializeField] Button playButton;
    public   float fadeDuration;

    [Header("Player Settings")]
    [SerializeField] Transform player;

    [Header("Main Menu Elements")]
    [SerializeField] GameObject allAssetsMainMenu;


    [SerializeField] GameObject chapter_One;
    [SerializeField] GameObject wizard_Canvas;
    void Start()
    {
        if (!isMainMenu) return;
        transitionImage.material.renderQueue = 3500;
        chapter1Icon.material.renderQueue = 3500;
        chapter1Icon.color = new Color(1, 1, 1, 0);
        chapter1Icon.gameObject.SetActive(false);
        chapter_One.SetActive(false);
        wizard_Canvas.SetActive(false);
        playButton.onClick.AddListener(PlayButtonClicked);
    }

    public void PlayButtonClicked() => StartCoroutine(FadeIn());

    IEnumerator FadeIn()
    {
        playButton.interactable = false;
        transitionImage.raycastTarget = true;
        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime * fadeDuration;
            Color tempColor = transitionImage.color;
            tempColor.a = Mathf.Lerp(0, 1, t);
            transitionImage.color = tempColor;
            yield return null;

        }
        foreach (var item in particles)
        {
            item.gameObject.SetActive(false);
        }
        yield return new WaitForSeconds(1);
        chapter1Icon.gameObject.SetActive(true);
        // t = 0f;
        // while (t < 1)
        // {
        //     t += Time.deltaTime * fadeDuration * .8f;
        //     Color tempColor = chapter1Icon.color;
        //     tempColor.a = Mathf.Lerp(0, 1, t);
        //     chapter1Icon.color = tempColor;
        //     yield return null;

        // }

        StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        float t = 0;


        yield return new WaitForSeconds(4f);
        //  while (t < 1)
        // {
        //     t += Time.deltaTime * fadeDuration * .8f;
        //     Color tempColor = chapter1Icon.color;
        //     tempColor.a = Mathf.Lerp(1, 0, t);
        //     chapter1Icon.color = tempColor;
        //     yield return null;

        // }

        chapter1Icon.gameObject.SetActive(false);
        allAssetsMainMenu.SetActive(false);
        chapter_One.SetActive(true);
        // t = 0f;

        while (t < 1)
        {
            t += Time.deltaTime * fadeDuration;
            Color tempColor = transitionImage.color;
            tempColor.a = Mathf.Lerp(1, 0, t);
            transitionImage.color = tempColor;
            yield return null;

        }
        transitionImage.raycastTarget = false;
        yield return new WaitForSeconds(1f);
        wizard_Canvas.SetActive(true);
        wizardDialogue.StartWizardDialogue();

    }
}
