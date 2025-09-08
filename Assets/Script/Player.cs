using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;

[RequireComponent(typeof(KnightControl))]
public class ClickMoveXWithSpine : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float stopDistance = 0.01f;
    public bool canMove;
    public GameObject handIcon;
    private Coroutine movementCoroutine = null;

    [Header("Buttons (UI)")]
    // Assign the three visible UI Buttons (Odd / Prime / Even)
    public Button oddButton;
    public Button primeButton;
    public Button evenButton;
    public RectTransform[] choiceButtonRects;      // RectTransforms to animate when popping (can be same as Buttons' rects)

    [Header("Text UI")]
    public TMP_Text questionText;           // "Which number is this?" (top banner) - TMP_Text works for both UI and 3D TMP
    public TMP_Text bigNumberText;         // Big number shown above gate (center)
    // store original color so we can restore after fades
private Color bigNumberOriginalColor = Color.white;


    [Header("Gameplay Settings")]
    public int minNumber = 1;
    public int maxNumber = 100;
    public int maxQuestions = 5;                  // how many numbers to ask in this level
    public float timeBetweenQuestions = 0.4f;     // delay after answering before next question
    public float numberFadeTime = 0.25f;  

    [Header("Typewriter Settings")]
    public float typingSpeed = 0.04f;             // seconds per character

    // Animation / facing
    private KnightControl knight;
    private bool facingRight = true;
    private string lastAnim = "";

    // Internal state for the question flow
    int currentQuestionIndex = 0;
    int currentNumber = 0;
    bool acceptingAnswer = false;

    void Start()
    {
        knight = GetComponent<KnightControl>();
        // Start idle
        PlayIdle();
        // At end of Start() or after you've referenced bigNumberText:
if (bigNumberText != null)
{
    bigNumberOriginalColor = bigNumberText.color;
}


        // Ensure initial facing matches skeleton scale
        if (knight != null && knight.skeleton != null)
            facingRight = knight.skeleton.ScaleX >= 0f;
        else
            facingRight = transform.localScale.x >= 0f;

        // Hide big number initially (keep questionText visible if you want)
        if (bigNumberText != null) bigNumberText.gameObject.SetActive(false);

        // Disable buttons at start
        if (oddButton != null) oddButton.interactable = false;
        if (primeButton != null) primeButton.interactable = false;
        if (evenButton != null) evenButton.interactable = false;

        // If no rects provided, try to use the buttons' rects
        if ((choiceButtonRects == null || choiceButtonRects.Length == 0))
        {
            List<RectTransform> rects = new List<RectTransform>();
            if (oddButton != null) rects.Add(oddButton.GetComponent<RectTransform>());
            if (primeButton != null) rects.Add(primeButton.GetComponent<RectTransform>());
            if (evenButton != null) rects.Add(evenButton.GetComponent<RectTransform>());
            choiceButtonRects = rects.ToArray();
        }

        // Disable animator (if present) until popping
        if (oddButton != null && oddButton.GetComponent<Animator>() != null) oddButton.GetComponent<Animator>().enabled = false;
        if (primeButton != null && primeButton.GetComponent<Animator>() != null) primeButton.GetComponent<Animator>().enabled = false;
        if (evenButton != null && evenButton.GetComponent<Animator>() != null) evenButton.GetComponent<Animator>().enabled = false;

        foreach (var item in choiceButtonRects)
        {
            item.GetComponent<Animator>().enabled = false;
        }
    }

    void Update()
    {
        if (canMove)
        {
            PlayerMovement();
        }
    }

    #region Movement & Spine
    public void StartGamePlayLevelOne()
    {
        // Enable movement control if needed
        canMove = true;
        StartCoroutine(PopButtonsThenStartQuestions());
    }

    IEnumerator PopButtonsThenStartQuestions()
    {
        // Hide question briefly while buttons pop (will show after)
        if (questionText != null)
            questionText.gameObject.SetActive(false);

        // pop each button scale from zero -> 1
        if (choiceButtonRects != null)
        {
            for (int i = 0; i < choiceButtonRects.Length; i++)
            {
                var r = choiceButtonRects[i];
                if (r == null) continue;
                r.localScale = Vector3.zero;
                r.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack).SetDelay(0.08f * i);
            }
            yield return new WaitForSeconds(0.08f * choiceButtonRects.Length + 0.25f);
        }

        // Show the question banner and type the prompt
        if (questionText != null)
        {
            questionText.gameObject.SetActive(true);
            yield return StartCoroutine(TypeText(questionText, "Which number is this?"));
        }

        // Enable any animators on buttons
        if (oddButton != null && oddButton.GetComponent<Animator>() != null) oddButton.GetComponent<Animator>().enabled = true;
        if (primeButton != null && primeButton.GetComponent<Animator>() != null) primeButton.GetComponent<Animator>().enabled = true;
        if (evenButton != null && evenButton.GetComponent<Animator>() != null) evenButton.GetComponent<Animator>().enabled = true;


        foreach (var item in choiceButtonRects)
        {
            item.GetComponent<Animator>().enabled = true;
        }
        // Start asking numbers
        currentQuestionIndex = 0;
        yield return new WaitForSeconds(0.15f);
        NextQuestion();
    }

    private void PlayerMovement()
    {
        // Mouse click / touch only processed when not clicking on UI
        // Mouse
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (handIcon != null && handIcon.activeInHierarchy)
                handIcon.SetActive(false);

            Vector3 mouse = Input.mousePosition;
            Vector3 world = Camera.main.ScreenToWorldPoint(mouse);

            Vector3 targetPosition = new Vector3(world.x, transform.position.y, transform.position.z);
            bool goingRight = targetPosition.x >= transform.position.x;
            SetFacing(goingRight);

            PlayRun();

            if (movementCoroutine != null)
                StopCoroutine(movementCoroutine);
            movementCoroutine = StartCoroutine(MoveToX(targetPosition.x));
        }

        // Touch
        if (Input.touchCount > 0)
        {
            Touch t = Input.touches[0];
            if (t.phase == TouchPhase.Began)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                    return;

                if (handIcon != null && handIcon.activeInHierarchy)
                    handIcon.SetActive(false);

                Vector3 world = Camera.main.ScreenToWorldPoint((Vector3)t.position);
                Vector3 targetPosition = new Vector3(world.x, transform.position.y, transform.position.z);
                bool goingRight = targetPosition.x >= transform.position.x;
                SetFacing(goingRight);

                PlayRun();

                if (movementCoroutine != null)
                    StopCoroutine(movementCoroutine);
                movementCoroutine = StartCoroutine(MoveToX(targetPosition.x));
            }
        }
    }

    IEnumerator MoveToX(float targetX)
    {
        while (Mathf.Abs(transform.position.x - targetX) > stopDistance)
        {
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetX, transform.position.y, transform.position.z), speed * Time.deltaTime);
            yield return null;
        }
        // Snap and idle
        transform.position = new Vector3(targetX, transform.position.y, transform.position.z);
        PlayIdle();

        // clear handle
        movementCoroutine = null;
    }

    private void SetFacing(bool right)
    {
        if (facingRight == right) return;
        facingRight = right;
        if (knight != null && knight.skeleton != null)
        {
            float abs = Mathf.Abs(knight.skeleton.ScaleX);
            knight.skeleton.ScaleX = right ? abs : -abs;
        }
        else
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
            transform.localScale = s;
        }
    }

    private void PlayRun()
    {
        if (lastAnim == "run") return;
        knight.running();
        lastAnim = "run";
    }

    private void PlayIdle()
    {
        if (lastAnim == "idle") return;
        knight.idle();
        lastAnim = "idle";
    }
    #endregion

    #region Question Flow
    void NextQuestion()
    {
        if (currentQuestionIndex >= maxQuestions)
        {
            EndQuestions();
            return;
        }

        currentQuestionIndex++;
        GenerateAndShowNumber();
    }

void GenerateAndShowNumber()
{
    currentNumber = Random.Range(minNumber, maxNumber + 1);

    // Show big number with fade-in & pop (enable object)
    if (bigNumberText != null)
    {
        bigNumberText.gameObject.SetActive(true);
        bigNumberText.text = currentNumber.ToString();

        // ensure color starts from transparent alpha
        Color c = bigNumberOriginalColor;
        c.a = 0f;
        bigNumberText.color = c;

        // start small then pop while fading in
        bigNumberText.transform.localScale = Vector3.one * 0.8f;
        var seq = DOTween.Sequence();
        seq.Append(bigNumberText.DOFade(1f, numberFadeTime));                  // fade alpha to 1
        seq.Join(bigNumberText.transform.DOScale(1.05f, 0.18f).SetEase(Ease.OutBack));
        seq.OnComplete(() =>
        {
            // small settle
            bigNumberText.transform.DOScale(1.0f, 0.07f);
            // restore full color (ensures exact original rgb)
            bigNumberText.color = bigNumberOriginalColor;
        });
    }

    // Enable buttons for answers
    acceptingAnswer = true;
    if (oddButton != null) oddButton.interactable = true;
    if (primeButton != null) primeButton.interactable = true;
    if (evenButton != null) evenButton.interactable = true;
}


    // PUBLIC methods to assign in Button.OnClick via Inspector
    public void OnOddPressed()   { HandleChoice(0); }
    public void OnPrimePressed() { HandleChoice(1); }
    public void OnEvenPressed()  { HandleChoice(2); }

    // Central handler
    void HandleChoice(int buttonIndex)
    {
        Debug.Log("Button pressed idx=" + buttonIndex + " acceptingAnswer=" + acceptingAnswer);
        if (!acceptingAnswer) return;

        bool correct = false;
        if (buttonIndex == 0) correct = (currentNumber % 2 != 0);
        else if (buttonIndex == 1) correct = IsPrime(currentNumber);
        else if (buttonIndex == 2) correct = (currentNumber % 2 == 0);
        if (correct)
        {
            Debug.Log($"Answer: RIGHT (num={currentNumber})");

            // disable input while handling correct feedback
            acceptingAnswer = false;
            if (oddButton != null) oddButton.interactable = false;
            if (primeButton != null) primeButton.interactable = false;
            if (evenButton != null) evenButton.interactable = false;

            // Visual feedback: flash color briefly then fade out
            if (bigNumberText != null)
            {
                // flash cyan quickly
                var flashSeq = DOTween.Sequence();
                flashSeq.Append(bigNumberText.DOColor(Color.cyan, 0.12f));
                flashSeq.Append(bigNumberText.DOColor(bigNumberOriginalColor, 0.12f));

                // then fade out smoothly and hide
                flashSeq.Append(bigNumberText.DOFade(0f, numberFadeTime).SetEase(Ease.InQuad));
                flashSeq.OnComplete(() =>
                {
                    // hide after fade
                    bigNumberText.gameObject.SetActive(false);

                    // proceed to next question after the configured delay
                    StartCoroutine(WaitAndNextQuestion(timeBetweenQuestions));
                });
            }
            else
            {
                // fallback if no bigNumberText assigned
                StartCoroutine(WaitAndNextQuestion(timeBetweenQuestions));
            }
        }

        else
        {
            Debug.Log($"Answer: WRONG (num={currentNumber})");
            if (bigNumberText != null)
            {
                var seq = DOTween.Sequence();
                seq.Append(bigNumberText.DOColor(Color.red, 0.12f));
                seq.Append(bigNumberText.DOColor(Color.white, 0.12f));
            }

            // shake screen
            if (Camera.main != null)
            {
                Camera.main.transform.DOShakePosition(0.25f, strength: new Vector3(0.5f, 0.5f, 0), vibrato: 20);
            }

            // ❌ Don't disable buttons, let user try again
            // ❌ Don't advance question
        }
    }

IEnumerator WaitAndNextQuestion(float delay)
{
    yield return new WaitForSeconds(delay);
    NextQuestion();
}


    void EndQuestions()
    {
        Debug.Log("Level 1 questions finished.");
        acceptingAnswer = false;
        if (bigNumberText != null) bigNumberText.gameObject.SetActive(false);
        // YOU: Add logic to open gate, play transition, start next chapter, etc.
    }

    IEnumerator TypeText(TMP_Text tmp, string text)
    {
        if (tmp == null) yield break;
        tmp.text = "";
        foreach (char c in text.ToCharArray())
        {
            tmp.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
    }

    bool IsPrime(int num)
    {
        if (num < 2) return false;
        if (num == 2) return true;
        if (num % 2 == 0) return false;
        int r = Mathf.FloorToInt(Mathf.Sqrt(num));
        for (int i = 3; i <= r; i += 2)
            if (num % i == 0) return false;
        return true;
    }
    #endregion
}
