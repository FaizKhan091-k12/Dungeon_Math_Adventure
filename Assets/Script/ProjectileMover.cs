using UnityEngine;

/// <summary>
/// Handles projectile movement, collision with player/ground,
/// and safe destruction with optional impact VFX.
/// Uses swept CircleCast to avoid tunneling when moving fast.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ProjectileMover : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 6f;
    public float maxLifetime = 6f;
    public float hitRadius = 0.35f;
    public LayerMask playerLayer;
    public LayerMask groundLayer;

    [Header("Optional VFX")]
    public GameObject impactVfxPrefab;

    // internal
    private Vector3 targetPos;
    private ClickMoveXWithSpine playerController;
    private bool initialized = false;
    private Vector3 prevPosition;

    public void Initialize(Vector3 targetWorldPosition, float projectileSpeed, ClickMoveXWithSpine playerRef)
    {
        targetPos = targetWorldPosition;
        speed = projectileSpeed;
        playerController = playerRef;
        initialized = true;
        prevPosition = transform.position;
        Destroy(gameObject, maxLifetime);
//
   //     Debug.Log($"[Projectile] Initialized toward {targetPos}, speed={speed}");
    }

    void Start()
    {
        // fallback init if Initialize not called
        if (!initialized)
        {
            playerController = FindObjectOfType<ClickMoveXWithSpine>();
            initialized = (playerController != null);
           // if (initialized) Debug.Log("[Projectile] Auto-initialized playerController fallback.");
        }

        prevPosition = transform.position;
    }

    void Update()
    {
        if (!initialized) return;

        Vector3 dir = (targetPos - transform.position);
        float dist = dir.magnitude;

        // If basically at target, schedule destroy
        if (dist <= 0.01f)
        {
            Invoke(nameof(DestroyWithImpact), 0.05f);
            enabled = false;
            return;
        }

        // compute move for this frame
        Vector3 move = dir.normalized * speed * Time.deltaTime;
        Vector3 nextPos = transform.position + move;

        // Swept collision check
        float castDistance = move.magnitude;
        if (castDistance > 0f)
        {
            int layerMask = playerLayer | groundLayer;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(prevPosition, hitRadius, (nextPos - prevPosition).normalized, castDistance, layerMask);

            if (hits != null && hits.Length > 0)
            {
                foreach (var h in hits)
                {
                    if (h.collider == null) continue;

                    // check player
                    if ((playerLayer.value & (1 << h.collider.gameObject.layer)) != 0)
                    {
                        var pc = h.collider.GetComponent<ClickMoveXWithSpine>();
                        if (pc == null)
                            pc = h.collider.GetComponentInParent<ClickMoveXWithSpine>();

                        if (pc != null)
                        {
                            if (!pc.IsInvulnerable)
                            {
                         //       Debug.Log("[Projectile] Swept hit player — ApplyHit()");
                                pc.ApplyHit();
                            }
                            else
                            {
                          //      Debug.Log("[Projectile] Swept hit player but invulnerable");
                            }
                        }
                        else
                        {
                          //  Debug.LogWarning("[Projectile] Swept hit object on player layer but no ClickMoveXWithSpine found.");
                        }

                        DestroyWithImpact();
                        return;
                    }

                    // check ground
                    if ((groundLayer.value & (1 << h.collider.gameObject.layer)) != 0)
                    {
                      //  Debug.Log("[Projectile] Swept hit ground - destroying.");
                        DestroyWithImpact();
                        return;
                    }
                }
            }
        }

        // Move projectile
        transform.position = nextPos;

        // orient sprite/mesh to velocity
        if (move.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // update prev pos for next frame
        prevPosition = transform.position;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Still keep trigger-based fallback (rare case)
       // Debug.Log($"[Projectile] OnTriggerEnter2D with {other.gameObject.name}");

        // Player
        if ((playerLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            var pc = other.GetComponent<ClickMoveXWithSpine>();
            if (pc != null && !pc.IsInvulnerable)
            {
              //  Debug.Log("[Projectile] Trigger hit player — ApplyHit()");
                pc.ApplyHit();
            }

            DestroyWithImpact();
            return;
        }

        // Ground
        if ((groundLayer.value & (1 << other.gameObject.layer)) != 0)
        {
           // Debug.Log("[Projectile] Trigger hit ground - destroying.");
            DestroyWithImpact();
        }
    }

    void DestroyWithImpact()
    {
        if (impactVfxPrefab != null)
            Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
