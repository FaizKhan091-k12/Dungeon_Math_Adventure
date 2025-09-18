using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.UI.ProceduralImage;


[RequireComponent(typeof(KnightControl))]
public class ClickMoveXWithSpine : MonoBehaviour
{
    public static ClickMoveXWithSpine Instance;
    public Animation ruinsAnimation;
    public Animation doorAnimation;
    [Header("Result Popup")]
public TMP_Text centerResultText;      // assign a centered TMP text in inspector
public float popupFadeInTime = 0.12f;
public float popupScaleUp = 1.25f;
public float popupScaleTime = 0.18f;
public float popupHoldTime = 0.6f;
public float popupFadeOutTime = 0.18f;
    public RuinsProgress ruinsProgress;
    [Header("Movement")]
    public float speed = 5f;
    public float stopDistance = 0.01f;
    public bool canMove;
    public GameObject handIcon;
    private Coroutine movementCoroutine = null;

    [Header("Player Health")]
    public ProceduralImage[] heartImages;             // assign hearts left->right or right->left depending on UI
    public float heartDecreaseAmount = 0.5f;// amount to subtract per hit (0.5 => half-heart)
    public int totalAllowedHits = 6;        // total hits until death (3 hearts * 2)
    public float stunDurationOnHit = 1.0f;  // seconds of stun
    public float invulnerabilityTime = 0.6f;// time after hit during which player is invulnerable
    public float heartFillTweenTime = 0.25f;// tween time for heart fill animation

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onPlayerHit;   // optional hook for SFX/VFX
    public UnityEngine.Events.UnityEvent onPlayerDied;  // optional hook for death

    // internal
    private int hitsTaken = 0;
    public bool isInvulnerable = false;
    public bool isDead = false;
    [Header("Player State")]
    public bool playerHasMoved = false;    // becomes true after first move (used by enemies)
                                           // seconds

    // internal
    private bool isStunned = false;

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
    public int maxQuestions = 5;                  // legacy, not used for two-phase flow
    public float timeBetweenQuestions = 0.4f;     // delay after answering before next question
    public float numberFadeTime = 0.25f;


    [Header("Blink Effect")]
    public Material knightMaterial;      // assign Knight material here
    public float blinkMin = 0f;          // minimum FillPhase
    public float blinkMax = 1f;          // maximum FillPhase
    public float blinkSpeed = 0.3f;      // time for one up/down blink cycle
    private Tween blinkTween;            // to store DOTween handle


    [Header("Typewriter Settings")]
    public float typingSpeed = 0.04f;             // seconds per character

    // Animation / facing
    public KnightControl knight;
    private bool facingRight = true;
    private string lastAnim = "";

    // Internal state for the question flow
    int currentQuestionIndex = 0; // (kept for legacy if used elsewhere)
    int currentNumber = 0;
    bool acceptingAnswer = false;
    // track current X target so we can resume run animation after stun
    private float movementTargetX;

    public bool temp;

    // Two-phase question flow settings
    [Header("Question Flow Settings")]
    public int parityCorrectRequired = 4;     // number of correct parity answers before switching to prime questions
    public int primeCorrectRequired = 4;      // number of correct prime answers to finish level

    enum QuestionPhase { Parity, Prime, Done }
    private QuestionPhase phase = QuestionPhase.Parity;
    private int parityCorrectCount = 0;
    private int primeCorrectCount = 0;
    private int parityQuestionIndex = 0; // how many parity questions shown
    private int primeQuestionIndex = 0;  // how many prime questions shown

    public GameObject you_Won_Text;
    void Awake()
    {
        Instance = this;
    }
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

        SetupPhaseUI();
    }


    void SetupPhaseUI()
    {
        phase = QuestionPhase.Parity;
        parityCorrectCount = 0;
        primeCorrectCount = 0;
        parityQuestionIndex = 0;
        primeQuestionIndex = 0;

        // Parity phase: enable odd/even buttons, hide prime button
        if (oddButton != null) { SetButtonLabel(oddButton, "Odd"); oddButton.gameObject.SetActive(true); }
        if (evenButton != null) { SetButtonLabel(evenButton, "Even"); evenButton.gameObject.SetActive(true); }
        if (primeButton != null) primeButton.gameObject.SetActive(false);

        // optional question banner
        if (questionText != null) questionText.text = $"Question 0/{parityCorrectRequired}: Odd or Even?";
    }

    void SwitchToPrimePhase()
    {
        phase = QuestionPhase.Prime;
        primeCorrectCount = 0;
        primeQuestionIndex = 0;

        // Repurpose odd/even as Yes/No for prime question
        if (oddButton != null) { SetButtonLabel(oddButton, "Yes"); oddButton.gameObject.SetActive(true); }
        if (evenButton != null) { SetButtonLabel(evenButton, "No"); evenButton.gameObject.SetActive(true); }
        if (primeButton != null) primeButton.gameObject.SetActive(false);

        // update question banner for prime start
        if (questionText != null) questionText.text = $"Question 0/{primeCorrectRequired}: Is it prime?";
    }

    void FinishQuestions()
    {
        phase = QuestionPhase.Done;
        EndQuestions();
    }

    // helper to set text label on a Button (TMP or legacy Text)
    void SetButtonLabel(Button b, string label)
    {
        if (b == null) return;
        TMP_Text tmp = b.GetComponentInChildren<TMP_Text>();
        if (tmp != null) { tmp.text = label; return; }
        Text t = b.GetComponentInChildren<Text>();
        if (t != null) t.text = label;
    }

    public bool IsInvulnerable => isInvulnerable; // public getter

    public void ApplyHit()
    {
        // ignore if dead or invulnerable
        if (isDead || isInvulnerable) return;

        hitsTaken++;
        UpdateHeartUI();
        onPlayerHit?.Invoke();

        // handle death
        if (hitsTaken >= totalAllowedHits)
        {
            isDead = true;

            // play death via wrapper so lastAnim updates
            PlayDeath();
            onPlayerDied?.Invoke();

            // fade back to Main Menu
            if (SceneTransition.Instance != null)
            {
                SceneTransition.Instance.FadeAndLoad("Demo");
            }

            // disable movement & input
            canMove = false;

            // stop movement coroutine
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
                movementCoroutine = null;
            }

            // disable question UI so user can't keep answering
            acceptingAnswer = false;
            if (oddButton != null) oddButton.interactable = false;
            if (primeButton != null) primeButton.interactable = false;
            if (evenButton != null) evenButton.interactable = false;

            onPlayerDied?.Invoke();
            return;
        }

        // not dead -> stun
        // stop movement immediately
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        // mark player as stunned and play stun anim using wrapper
        PlayStun();

        // ensure lastAnim won't block next transitions (defensive)
        lastAnim = "stun";

        // start stun coroutine
        StartCoroutine(StunCoroutine(stunDurationOnHit));
    }

    IEnumerator StunCoroutine(float duration)
    {
        AudioManager.Instance.Stun();
        // make player invulnerable for a short while to avoid double hits
        isInvulnerable = true;

        // remember previous movement state and disable movement while stunned
        bool prevCanMove = canMove;
        canMove = false;
        isStunned = true;

        // START BLINK
        if (knightMaterial != null)
        {
            if (blinkTween != null && blinkTween.IsActive()) blinkTween.Kill();
            float startVal = knightMaterial.HasProperty("_FillPhase") ? knightMaterial.GetFloat("_FillPhase") : blinkMin;
            knightMaterial.SetFloat("_FillPhase", startVal);
            blinkTween = DOTween.To(
                () => knightMaterial.GetFloat("_FillPhase"),
                x => knightMaterial.SetFloat("_FillPhase", x),
                blinkMax,
                blinkSpeed
            ).SetLoops(-1, LoopType.Yoyo);
        }

        // Wait stun duration
        yield return new WaitForSeconds(duration);

        // End stun: stop blink and ensure idle animation is applied properly
        if (blinkTween != null && blinkTween.IsActive()) blinkTween.Kill();
        blinkTween = null;
        if (knightMaterial != null && knightMaterial.HasProperty("_FillPhase"))
            knightMaterial.SetFloat("_FillPhase", blinkMin);

        // Force lastAnim reset so PlayIdle/PlayRun will not early-return incorrectly
        lastAnim = "";

        if (!isDead)
        {
            // Force Idle — wrapper will set lastAnim = "idle"
            PlayIdle();
        }

        // restore movement permission (only if player wasn't dead previously)
        canMove = prevCanMove && !isDead;
        isStunned = false;

        // small invulnerability buffer to avoid immediate consecutive hits
        yield return new WaitForSeconds(invulnerabilityTime);
        isInvulnerable = false;

        // After invuln, decide whether to play Run or Idle based on distance
        if (!isDead && canMove)
        {
            float distToTarget = Mathf.Abs(transform.position.x - movementTargetX);
            if (distToTarget > stopDistance)
            {
                // movementTargetX indicates the player was moving — start Run
                PlayRun();
                // optionally start movement coroutine again toward target (if you want auto-resume)
                if (movementCoroutine == null)
                    movementCoroutine = StartCoroutine(MoveToX(movementTargetX));
            }
            else
            {
                PlayIdle();
            }
        }
    }

    // Reduce heart UI fill by heartDecreaseAmount from last heart to first
    void UpdateHeartUI()
    {
        if (heartImages == null || heartImages.Length == 0) return;

        // iterate from last heart to first (third -> second -> first)
        for (int i = heartImages.Length - 1; i >= 0; i--)
        {
            var img = heartImages[i];
            if (img == null) continue;

            if (img.fillAmount > 0f)
            {
                float target = Mathf.Max(0f, img.fillAmount - heartDecreaseAmount);
                // animate fill using DOTween if available
                if (DG.Tweening.DOTween.IsTweening(img)) { /* no-op; safe guard */ }

                img.DOFillAmount(target, heartFillTweenTime).SetEase(Ease.OutCubic);
                break;
            }
        }
    }


    void Update()
    {
        if (canMove)
        {
            PlayerMovement();
        }

        if (playerHasMoved)
        {
            if (temp)
            {
                StartCoroutine(PopButtonsThenStartQuestions());
                MainMenuController.Instacne.WizardDialoguesFinish();
                temp = false;
            }

        }
    }

    #region Movement & Spine
    public void StartGamePlayLevelOne()
    {
        // Enable movement control if needed
        canMove = true;
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
            yield return StartCoroutine(TypeText(questionText, $"Question {parityQuestionIndex}/{parityCorrectRequired}: Odd or Even?"));
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

            // before starting movement
            playerHasMoved = true; // mark that player moved at least once

            // store the movement target so we can check later
            movementTargetX = targetPosition.x;

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

                movementTargetX = targetPosition.x;

                if (movementCoroutine != null)
                    StopCoroutine(movementCoroutine);
                movementCoroutine = StartCoroutine(MoveToX(targetPosition.x));
            }
        }
    }

    IEnumerator MoveToX(float targetX)
    {
        PlayRun();
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
        // clear movement target (we're exactly at target now)
        movementTargetX = transform.position.x;
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

        // make sure rotation z stays zero (prevents accidental 90° rotation)
        Vector3 e = transform.eulerAngles;
        e.z = 0f;
        transform.eulerAngles = e;
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
    private void PlayStun()
    {
        if (lastAnim == "stun") return;
        if (knight != null) knight.stun();
        lastAnim = "stun";
    }


    private void PlayDeath()
    {
        if (lastAnim == "death") return;
        if (knight != null) knight.death();
        lastAnim = "death";
        AudioManager.Instance.Died();
        MainMenuController.Instacne.audio_LevelOne.volume = 0f;
    }

    public void PlayBuff()
    {
        if (lastAnim == "buff") return;
        if (knight != null) knight.skill_3();
        lastAnim = "buff";
    }
    #endregion

    #region Question Flow
    void NextQuestion()
    {
        if (isDead) return;

        // If we're in parity and parityCorrectCount already reached requirement, start prime
        if (phase == QuestionPhase.Parity && parityCorrectCount >= parityCorrectRequired)
        {
            SwitchToPrimePhase();
        }

        if (phase == QuestionPhase.Parity)
        {
            GenerateAndShowNumber();
        }
        else if (phase == QuestionPhase.Prime)
        {
            if (primeCorrectCount >= primeCorrectRequired)
            {
                FinishQuestions();
                return;
            }
            GenerateAndShowNumber();
        }
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

        // Enable appropriate buttons for answers depending on phase
        acceptingAnswer = true;
        if (phase == QuestionPhase.Parity)
        {
            parityQuestionIndex++;
            if (oddButton != null) oddButton.interactable = true;
            if (evenButton != null) evenButton.interactable = true;
            if (primeButton != null) primeButton.interactable = false;
            if (questionText != null) questionText.text = $"Question {parityQuestionIndex}/{parityCorrectRequired}: Odd or Even?";
        }
        else if (phase == QuestionPhase.Prime)
        {
            primeQuestionIndex++;
            if (oddButton != null) oddButton.interactable = true;
            if (evenButton != null) evenButton.interactable = true;
            if (primeButton != null) primeButton.interactable = false;
            if (questionText != null) questionText.text = $"Question {primeQuestionIndex}/{primeCorrectRequired}: Is it prime?";
        }
    }

    // PUBLIC methods to assign in Button.OnClick via Inspector
    public void OnOddPressed() { HandleChoice(0); }
    public void OnPrimePressed() { HandleChoice(1); }
    public void OnEvenPressed() { HandleChoice(2); }

    // Central handler
    void HandleChoice(int buttonIndex)
    {
        if (isDead) return;
        if (!acceptingAnswer) return;

        bool correct = false;

        if (phase == QuestionPhase.Parity)
        {
            if (buttonIndex == 0) correct = (currentNumber % 2 != 0);
            else if (buttonIndex == 2) correct = (currentNumber % 2 == 0);
            else correct = false;
        }
        else if (phase == QuestionPhase.Prime)
        {
            if (buttonIndex == 0) // Yes => prime
                correct = IsPrime(currentNumber);
            else if (buttonIndex == 2) // No => not prime
                correct = !IsPrime(currentNumber);
        }

        if (correct)
        {
            AudioManager.Instance.CorrectAnswer();
            ruinsProgress.OnCorrectAnswer();
ShowResultPopup("Correct!", Color.green);

            acceptingAnswer = false;
            if (oddButton != null) oddButton.interactable = false;
            if (primeButton != null) primeButton.interactable = false;
            if (evenButton != null) evenButton.interactable = false;

            if (phase == QuestionPhase.Parity)
                parityCorrectCount++;
            else if (phase == QuestionPhase.Prime)
                primeCorrectCount++;

            // If parity just reached requirement, immediately switch UI to prime phase
            if (phase == QuestionPhase.Parity && parityCorrectCount >= parityCorrectRequired)
            {
                SwitchToPrimePhase();
            }

            // Visual feedback: flash color briefly then fade out
            if (bigNumberText != null)
            {
                var flashSeq = DOTween.Sequence();
                flashSeq.Append(bigNumberText.DOColor(Color.cyan, 0.12f));
                flashSeq.Append(bigNumberText.DOColor(bigNumberOriginalColor, 0.12f));

                flashSeq.Append(bigNumberText.DOFade(0f, numberFadeTime).SetEase(Ease.InQuad));
                flashSeq.OnComplete(() =>
                {
                    bigNumberText.gameObject.SetActive(false);
                    StartCoroutine(WaitAndNextQuestion(timeBetweenQuestions));
                });
            }
            else
            {
                StartCoroutine(WaitAndNextQuestion(timeBetweenQuestions));
            }
        }
        else
        {
            AudioManager.Instance.WrongAnswer();
            ShowResultPopup("Wrong! Try again", Color.red);

            if (bigNumberText != null)
            {
                var seq = DOTween.Sequence();
                seq.Append(bigNumberText.DOColor(Color.red, 0.12f));
                seq.Append(bigNumberText.DOColor(Color.white, 0.12f));
            }

            if (Camera.main != null)
            {
                Camera.main.transform.DOShakePosition(0.25f, strength: new Vector3(0.5f, 0.5f, 0), vibrato: 20);
            }

            float aggressionReduction = 0.35f;
            EnemyController[] enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
                e.IncreaseAggression(aggressionReduction);

            // allow retry, do not advance question
        }
    }

    IEnumerator WaitAndNextQuestion(float delay)
    {
        yield return new WaitForSeconds(delay);
        NextQuestion();
    }

    void EndQuestions()
    {
        MainMenuController.Instacne.audio_LevelOne.volume = 0f;
        AudioManager.Instance.LevelOver();
        Debug.Log("Level 1 questions finished.");
       
        acceptingAnswer = false;
        if (oddButton != null) oddButton.interactable = false;
        if (primeButton != null) primeButton.interactable = false;
        if (evenButton != null) evenButton.interactable = false;

        if (bigNumberText != null) bigNumberText.gameObject.SetActive(false);

        canMove = false;
        DestroyAllProjectiles();
        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        foreach (var e in enemies)
        {
            e.Die();
        }
        if (!isDead)
        {
            ruinsAnimation.Play();
        }
    }

    [Header("Door Shake Settings")]
    public bool shakeScreenDuringDoor = true;
    public float doorShakeStrength = 0.25f;    // inspector control for magnitude
    public float doorShakeDuration = 4.5f;     // duration of the shake (defaults to previous value)
    public int doorShakeVibrato = 10;          // vibrato for DOShakePosition

    private Tween doorShakeTween = null;

    public void PlayDoorAnimation()
    {
        if (doorAnimation != null)
            doorAnimation.Play();

        if (shakeScreenDuringDoor && Camera.main != null)
        {
            if (doorShakeTween != null && doorShakeTween.IsActive()) doorShakeTween.Kill();
            doorShakeTween = Camera.main.transform.DOShakePosition(doorShakeDuration, new Vector3(doorShakeStrength, doorShakeStrength, 0f), doorShakeVibrato, randomnessMode: ShakeRandomnessMode.Full);
            StartCoroutine(StopDoorShakeWhenAnimationEnds());
        }
    }

    IEnumerator StopDoorShakeWhenAnimationEnds()
    {
        float timer = 0f;
        float maxWait = doorShakeDuration + 0.1f;
        while ((doorAnimation != null && doorAnimation.isPlaying) && timer < maxWait)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (doorShakeTween != null && doorShakeTween.IsActive())
        {
            doorShakeTween.Kill();
            doorShakeTween = null;
        }

        yield break;
    }

    public void CameraShake(float duration, Vector3 strength, int vibrato = 20)
    {
        if (Camera.main == null) return;
        DOTween.Kill(Camera.main.transform);
        Camera.main.transform.DOShakePosition(duration, strength, vibrato, randomnessMode: ShakeRandomnessMode.Full);
    }

    void DestroyAllProjectiles()
    {
        GameObject[] projectiles = GameObject.FindGameObjectsWithTag("Projectile");
        foreach (var p in projectiles)
        {
            Destroy(p);
        }
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

    /// <summary>
    /// Force the player to abandon current state and walk to `portalPos`.
    /// First moves on X (playing Run), then moves on Y (playing Idle by default).
    /// Disables player input while moving. Use StartCoroutine(GoToPortal(target)) to call.
    /// </summary>
    public IEnumerator GoToPortal(Vector3 portalPos, float moveSpeedOverride = -1f, float arrivalThreshold = 0.02f, float pauseAfterX = 0.12f)
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        if (blinkTween != null && blinkTween.IsActive())
        {
            blinkTween.Kill();
            blinkTween = null;
        }
        if (knightMaterial != null && knightMaterial.HasProperty("_FillPhase"))
            knightMaterial.SetFloat("_FillPhase", blinkMin);

        canMove = false;
        acceptingAnswer = false;

        bool faceRight = portalPos.x >= transform.position.x;
        SetFacing(faceRight);

        float originalSpeed = speed;
        if (moveSpeedOverride > 0f) speed = moveSpeedOverride;

        movementTargetX = portalPos.x;
        PlayRun();

        while (Mathf.Abs(transform.position.x - portalPos.x) > arrivalThreshold)
        {
            float step = speed * Time.deltaTime;
            float newX = Mathf.MoveTowards(transform.position.x, portalPos.x, step);
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);

            Vector3 e = transform.eulerAngles;
            e.z = 0f;
            transform.eulerAngles = e;

            yield return null;
        }

        transform.position = new Vector3(portalPos.x, transform.position.y, portalPos.z);

        yield return new WaitForSeconds(pauseAfterX);

        PlayIdle();

        while (Mathf.Abs(transform.position.y - portalPos.y) > arrivalThreshold)
        {
            float step = speed * Time.deltaTime;
            float newY = Mathf.MoveTowards(transform.position.y, portalPos.y, step);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            yield return null;
        }

        transform.position = new Vector3(portalPos.x, portalPos.y, portalPos.z);

        if (moveSpeedOverride > 0f) speed = originalSpeed;

        PlayBuff();
        you_Won_Text.SetActive(true);
    }

 // single tween sequence handle so we can kill/restart
private Tween resultPopupTween = null;

/// <summary>
/// Show a centered popup message (fades & pops) — auto hides.
/// Call ShowResultPopup("Correct!", Color.green) or ShowResultPopup("Wrong! Try again", Color.red).
/// </summary>
public void ShowResultPopup(string message, Color color)
{
    if (centerResultText == null) return;

    // kill previous tween
    if (resultPopupTween != null && resultPopupTween.IsActive()) resultPopupTween.Kill();

    centerResultText.gameObject.SetActive(true);
    centerResultText.text = message;
    centerResultText.color = new Color(color.r, color.g, color.b, 0f);

    // ensure starting scale & alpha
    centerResultText.transform.localScale = Vector3.one * 0.9f;

    // build sequence
    var seq = DOTween.Sequence();

    // fade in alpha to 1 while scaling up slightly
    seq.Append(
        DOTween.To(() => centerResultText.color.a,
                   x => {
                       var c = centerResultText.color;
                       c.a = x;
                       centerResultText.color = c;
                   },
                   1f, popupFadeInTime)
    );
    seq.Join(centerResultText.transform.DOScale(popupScaleUp, popupScaleTime).SetEase(Ease.OutBack));

    // small settle back to 1.0 scale
    seq.Append(centerResultText.transform.DOScale(1f, 0.08f).SetEase(Ease.OutQuad));

    // hold visible
    seq.AppendInterval(popupHoldTime);

    // fade out and shrink a bit
    seq.Append(
        DOTween.To(() => centerResultText.color.a,
                   x => {
                       var c = centerResultText.color;
                       c.a = x;
                       centerResultText.color = c;
                   },
                   0f, popupFadeOutTime)
    );
    seq.Join(centerResultText.transform.DOScale(0.9f, popupFadeOutTime));

    seq.OnComplete(() =>
    {
        // make sure it's hidden and reset color alpha
        var c = centerResultText.color;
        c.a = 0f;
        centerResultText.color = c;
        centerResultText.gameObject.SetActive(false);
    });

    resultPopupTween = seq;
}

}
