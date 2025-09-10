using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.UI.ProceduralImage;
using UnityEditor.Rendering;

[RequireComponent(typeof(KnightControl))]
public class ClickMoveXWithSpine : MonoBehaviour
{
    public static ClickMoveXWithSpine Instance;
    public Animation ruinsAnimation;
    public Animation doorAnimation;
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
    public int maxQuestions = 5;                  // how many numbers to ask in this level
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
    int currentQuestionIndex = 0;
    int currentNumber = 0;
    bool acceptingAnswer = false;
    // track current X target so we can resume run animation after stun
    private float movementTargetX;

    public bool temp;

    void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        knight = GetComponent<KnightControl>();
        // Start idle
        PlayIdle();

        //   Debug.Log($"[Player] ClickMoveXWithSpine started. knight={(knight != null)}, gameObject.layer={gameObject.layer}");
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

            // before starting movement
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

                // ... after computing targetPosition and PlayRun()
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
    public void OnOddPressed() { HandleChoice(0); }
    public void OnPrimePressed() { HandleChoice(1); }
    public void OnEvenPressed() { HandleChoice(2); }

    // Central handler
    void HandleChoice(int buttonIndex)
    {
        if (isDead) return;
        //   Debug.Log("Button pressed idx=" + buttonIndex + " acceptingAnswer=" + acceptingAnswer);
        if (!acceptingAnswer) return;

        bool correct = false;
        if (buttonIndex == 0) correct = (currentNumber % 2 != 0);
        else if (buttonIndex == 1) correct = IsPrime(currentNumber);
        else if (buttonIndex == 2) correct = (currentNumber % 2 == 0);
        if (correct)
        {
            AudioManager.Instance.CorrectAnswer();
            //    Debug.Log($"Answer: RIGHT (num={currentNumber})");
            ruinsProgress.OnCorrectAnswer();
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
            AudioManager.Instance.WrongAnswer();
            //Debug.Log($"Answer: WRONG (num={currentNumber})");
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

            // Example: reduce enemy attack interval by 0.35 seconds on each wrong answer
            float aggressionReduction = 0.35f;

            // call on all enemies in scene (or you can maintain a list of enemies in the level)
            EnemyController[] enemies = FindObjectsOfType<EnemyController>();
            foreach (var e in enemies)
                e.IncreaseAggression(aggressionReduction);



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
        MainMenuController.Instacne.audio_LevelOne.volume = 0f;
        AudioManager.Instance.LevelOver();
        Debug.Log("Level 1 questions finished.");

        acceptingAnswer = false;
        if (oddButton != null) oddButton.interactable = false;
        if (primeButton != null) primeButton.interactable = false;
        if (evenButton != null) evenButton.interactable = false;

        if (bigNumberText != null) bigNumberText.gameObject.SetActive(false);

        // stop player movement if you want them to watch enemies die / move to next scene
        canMove = false;
        DestroyAllProjectiles();
        // find all enemies in scene and tell them to die
        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        foreach (var e in enemies)
        {
            e.Die();
        }
        if (!isDead)
        {

            ruinsAnimation.Play();

        }

        // Optionally open gate, play a celebration, start next chapter, etc.
        // Example: StartCoroutine(OpenGateAndContinue());
    }

    public void PlayDoorAnimation()
    {
        doorAnimation.Play();
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
    // Stop any current movement and states
    if (movementCoroutine != null)
    {
        StopCoroutine(movementCoroutine);
        movementCoroutine = null;
    }

    // Stop blink and invuln visuals
    if (blinkTween != null && blinkTween.IsActive())
    {
        blinkTween.Kill();
        blinkTween = null;
    }
    if (knightMaterial != null && knightMaterial.HasProperty("_FillPhase"))
        knightMaterial.SetFloat("_FillPhase", blinkMin);

    // Immediately prevent gameplay input and question flow
    canMove = false;
    acceptingAnswer = false;

    // Stop any typing or question coroutines if needed
    // (If you have references to them, stop here. We assume StartCoroutine(TypeText(...)) isn't stored.)

    // Face the target X
    bool faceRight = portalPos.x >= transform.position.x;
    SetFacing(faceRight);

    // Use override speed if provided
    float originalSpeed = speed;
    if (moveSpeedOverride > 0f) speed = moveSpeedOverride;

    // Force run animation and track movement target
    movementTargetX = portalPos.x;
    PlayRun();

    // Move along X axis until reached
    while (Mathf.Abs(transform.position.x - portalPos.x) > arrivalThreshold)
    {
        // move only in X
        float step = speed * Time.deltaTime;
        float newX = Mathf.MoveTowards(transform.position.x, portalPos.x, step);
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);

        // ensure no accidental rotation
        Vector3 e = transform.eulerAngles;
        e.z = 0f;
        transform.eulerAngles = e;

        yield return null;
    }

    // Snap X exactly
    transform.position = new Vector3(portalPos.x, transform.position.y, portalPos.z);

    // tiny pause so it feels like player arrived
    yield return new WaitForSeconds(pauseAfterX);

    // Now move on Y (climb into portal). We'll play Idle (or you can change to a climb animation)
    // Optionally face Y direction doesn't matter for X-facing, but ensure facing correct X.
    PlayIdle();

    // Move Y to portal.y
    while (Mathf.Abs(transform.position.y - portalPos.y) > arrivalThreshold)
    {
        float step = speed * Time.deltaTime;
        float newY = Mathf.MoveTowards(transform.position.y, portalPos.y, step);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        yield return null;
    }

    // Snap final position
    transform.position = new Vector3(portalPos.x, portalPos.y, portalPos.z);

    // restore speed if we overrode it
    if (moveSpeedOverride > 0f) speed = originalSpeed;

        // final idle (or you can trigger a 'enter portal' animation or call a level end)
        PlayBuff();

    // (Optional) allow external code to continue; we keep player input disabled so the scene end can play.
    // If you want to re-enable movement after arriving, set canMove = true here.
}

}
