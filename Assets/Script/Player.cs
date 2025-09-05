using UnityEngine;
using Spine.Unity;

[RequireComponent(typeof(KnightControl))]
public class ClickMoveXWithSpine : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float stopDistance = 0.01f;

    private Vector3 targetPosition;
    private bool isMoving = false;

    // Animation / facing
    private KnightControl knight;
    private bool facingRight = true;
    private string lastAnim = "";

    void Start()
    {
        knight = GetComponent<KnightControl>();
        targetPosition = transform.position;

        PlayIdle(); // start idle
        // Ensure initial facing matches skeleton scale
        if (knight.skeleton != null)
            facingRight = knight.skeleton.ScaleX >= 0f;
        else
            facingRight = transform.localScale.x >= 0f;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouse = Input.mousePosition;
            Vector3 world = Camera.main.ScreenToWorldPoint(mouse);

            // Lock to X only
            targetPosition = new Vector3(world.x, transform.position.y, transform.position.z);
            isMoving = true;

            // Face the direction of travel
            bool goingRight = targetPosition.x >= transform.position.x;
            SetFacing(goingRight);

            // Start running if not already
            PlayRun();
        }

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

            if (Mathf.Abs(transform.position.x - targetPosition.x) <= stopDistance)
            {
                isMoving = false;
                // Snap exactly to avoid tiny drift
                transform.position = new Vector3(targetPosition.x, transform.position.y, transform.position.z);

                // Transition to idle
                PlayIdle();
            }
        }
    }

    private void SetFacing(bool right)
    {
        if (facingRight == right) return;
        facingRight = right;

        // Prefer flipping the Spine skeleton (works with Spine attachments)
        if (knight != null && knight.skeleton != null)
        {
            float abs = Mathf.Abs(knight.skeleton.ScaleX);
            knight.skeleton.ScaleX = right ? abs : -abs;
        }
        else
        {
            // Fallback: flip transform scale
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
}
