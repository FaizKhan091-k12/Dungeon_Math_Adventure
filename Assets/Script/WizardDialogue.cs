using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class WizardDialogue : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dialoguePanel;          // Panel that contains the dialogue UI
    public TextMeshProUGUI dialogueText;      // The main dialogue text (TextMeshPro)
    public TextMeshProUGUI tapToContinueText; // "Tap to Continue" text (TextMeshPro)

    [Header("Dialogue Settings")]
    [TextArea(3, 6)]
    public string[] dialogues;                // Lines to show
    public float typingSpeed = 0.04f;         // Time between characters (seconds)
    public float tapBlinkInterval = 0.6f;     // Blink speed for "Tap to Continue"
    public bool acceptRawInput = true;        // Accept Mouse/Touch input

    [Header("Rotation (while dialogue plays)")]
    public Transform rotateTarget;            // Transform to wobble (assign wizard portrait or panel)
    public float minZ = -6f;                  // minimum random Z rotation (degrees)
    public float maxZ = 6f;                   // maximum random Z rotation (degrees)
    public float rotateSpeed = 180f;          // degrees per second while rotating to target
    public float changeInterval = 0.6f;       // how often to pick a new random target
    public bool smoothReturnOnEnd = true;     // smoothly return to 0 when done
    public float returnSpeed = 300f;          // speed to return to 0 after end

    [Header("Events")]
    public UnityEvent onDialogueFinished;     // Hook this to start the level / gameplay

    // Internal
    int currentIndex = 0;
    bool isTyping = false;
    Coroutine typingCoroutine;
    Coroutine blinkCoroutine;
    Coroutine rotateCoroutine;

    void Start()
    {
       // if (dialoguePanel != null) dialoguePanel.SetActive(false);
        //if (tapToContinueText != null) tapToContinueText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (acceptRawInput)
        {
            if (Input.GetMouseButtonDown(0))
                OnPlayerTap();

            if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began)
                OnPlayerTap();
        }
    }

    // --- Dialogue control ---
    public void StartWizardDialogue()
    {
        if (dialogues == null || dialogues.Length == 0)
        {
            EndDialogue();
            return;
        }

        currentIndex = 0;
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        ShowCurrentLine();

        // start random rotation if target assigned
        if (rotateTarget != null)
        {
            if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
            rotateCoroutine = StartCoroutine(RotateRandomly());
        }
    }

    public void OnPlayerTap()
    {
        // If typing -> finish instantly
        if (isTyping)
        {
            FinishTypingInstantly();
            return;
        }

        // advance or end
        if (dialogues != null && currentIndex < dialogues.Length - 1)
        {
            currentIndex++;
            ShowCurrentLine();
        }
        else
        {
            EndDialogue();
        }
    }

    void ShowCurrentLine()
    {
        if (tapToContinueText != null) tapToContinueText.gameObject.SetActive(false);
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(dialogues[currentIndex]));
    }

    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = string.Empty;

        foreach (char c in line)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;

        if (tapToContinueText != null)
        {
            tapToContinueText.gameObject.SetActive(true);
            if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
            blinkCoroutine = StartCoroutine(BlinkTapText());
        }
    }

    void FinishTypingInstantly()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        dialogueText.text = dialogues[currentIndex];
        isTyping = false;

        if (tapToContinueText != null)
        {
            tapToContinueText.gameObject.SetActive(true);
            if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
            blinkCoroutine = StartCoroutine(BlinkTapText());
        }
    }

    IEnumerator BlinkTapText()
    {
        while (true)
        {
            if (tapToContinueText != null) tapToContinueText.alpha = 1f;
            yield return new WaitForSeconds(tapBlinkInterval);
            if (tapToContinueText != null) tapToContinueText.alpha = 0.15f;
            yield return new WaitForSeconds(tapBlinkInterval);
        }
    }

    void EndDialogue()
    {
        // stop typing and blinking
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (tapToContinueText != null) tapToContinueText.gameObject.SetActive(false);

        // stop rotation coroutine and optionally return to zero
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        rotateCoroutine = null;

        if (rotateTarget != null && smoothReturnOnEnd)
            StartCoroutine(SmoothReturnRotation());

        onDialogueFinished?.Invoke();
    }

    // --- Rotation Coroutine ---
    IEnumerator RotateRandomly()
    {
        // keep current angle as the starting point
        float currentZ = rotateTarget.localEulerAngles.z;
        // convert to signed angle [-180,180]
        currentZ = NormalizeAngle(currentZ);

        float targetZ = currentZ;

        while (true)
        {
            // pick a new random target angle in range
            targetZ = Random.Range(minZ, maxZ);

            // rotate smoothly toward target angle
            while (Mathf.Abs(Mathf.DeltaAngle(currentZ, targetZ)) > 0.1f)
            {
                currentZ = Mathf.MoveTowardsAngle(currentZ, targetZ, rotateSpeed * Time.deltaTime);
                SetLocalZ(rotateTarget, currentZ);
                yield return null;
            }

            // small pause at target before picking another
            yield return new WaitForSeconds(changeInterval);
        }
    }

    IEnumerator SmoothReturnRotation()
    {
        // smoothly move current rotation back to 0
        float currentZ = NormalizeAngle(rotateTarget.localEulerAngles.z);
        while (Mathf.Abs(currentZ) > 0.5f)
        {
            currentZ = Mathf.MoveTowardsAngle(currentZ, 0f, returnSpeed * Time.deltaTime);
            SetLocalZ(rotateTarget, currentZ);
            yield return null;
        }

        // finally snap exactly to zero
        SetLocalZ(rotateTarget, 0f);
    }

    // helpers
    void SetLocalZ(Transform t, float z)
    {
        Vector3 e = t.localEulerAngles;
        e.z = z;
        t.localEulerAngles = e;
    }

    float NormalizeAngle(float a)
    {
        a = Mathf.Repeat(a + 180f, 360f) - 180f; // map to [-180,180]
        return a;
    }

    // Convenience alias if you prefer a differently named OnClick hookup
    public void OnScreenTap()
    {
        OnPlayerTap();
    }
}
