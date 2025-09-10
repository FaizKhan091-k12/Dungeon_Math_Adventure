using System.Collections;
using UnityEngine;
using Spine.Unity;

public class EnemyController : MonoBehaviour
{
    [Header("Attack / Projectile")]
    public Transform firePoint;
    public GameObject projectilePrefab;
    public float attackInterval = 3f;
    public float attackIntervalMin = 0.6f;
    public float firstAttackDelay = 1f;
    [Tooltip("Delay from starting attack anim until projectile spawn (mid-animation).")]
    public float projectileSpawnDelay = 0.35f;

    [Header("Animator / Spine")]
    public Animator animator;
    public string attackTrigger = "Attack";
    public string deathTriggerName = "Death";

    // Spine fallback
    Spine.Unity.SkeletonAnimation spineSkeleton;
    public string spineAttackAnim = "attack";
    public string spineDeathAnim = "death";

    [Header("Death / cleanup")]
    public float deathDestroyDelay = 2f; // optional destroy delay after die

    // internal state
    ClickMoveXWithSpine playerController;
    float nextAttackTime;
    private bool isDead = false;       // true when Die() called
    private bool isStopped = false;    // true when StopAttacking() called (stop but no death)
    private Coroutine attackCoroutineRef = null;

    void Start()
    {
        playerController = FindObjectOfType<ClickMoveXWithSpine>();
        spineSkeleton = GetComponent<SkeletonAnimation>();
        if (animator == null && spineSkeleton == null)
            animator = GetComponent<Animator>();

        // schedule first attack after firstAttackDelay
        nextAttackTime = Time.time + Mathf.Max(0f, firstAttackDelay);

        // start attack loop and keep reference
        attackCoroutineRef = StartCoroutine(AttackLoop());
    }

    IEnumerator AttackLoop()
    {
        // Wait until player has moved at least once
        while (playerController == null || !playerController.playerHasMoved)
        {
            // if we get stopped or die while waiting, exit
            if (isStopped || isDead) yield break;
            yield return null;
        }

        // Normal attack loop
        while (true)
        {
            if (isStopped || isDead) break;

            float wait = Mathf.Max(0f, nextAttackTime - Time.time);
            if (wait > 0f)
            {
                // Wait in small chunks so we can respond to Stop/Die promptly
                float elapsed = 0f;
                while (elapsed < wait)
                {
                    if (isStopped || isDead) yield break;
                    float step = Mathf.Min(0.2f, wait - elapsed);
                    yield return new WaitForSeconds(step);
                    elapsed += step;
                }
            }

            if (isStopped || isDead) break;

            // start attack animation
            if (animator != null)
            {
                animator.SetTrigger(attackTrigger);
            }
            else if (spineSkeleton != null)
            {
                spineSkeleton.AnimationState.SetAnimation(0, spineAttackAnim, false);
            }

            // wait a bit so animation reaches spawn point (but allow interrupt)
            float elapsedDelay = 0f;
            while (elapsedDelay < projectileSpawnDelay)
            {
                if (isStopped || isDead) break;
                float step = Mathf.Min(0.02f, projectileSpawnDelay - elapsedDelay);
                yield return new WaitForSeconds(step);
                elapsedDelay += step;
            }

            if (isStopped || isDead)
            {
                // skip spawn and exit loop if we were stopped/died mid-attack
                break;
            }

            // capture player last position and spawn projectile (if allowed)
            if (firePoint != null && projectilePrefab != null && playerController != null)
            {
                Vector3 targetPos = playerController.transform.position; // last known player pos at spawn moment
                SpawnProjectileTowards(targetPos);
            }

            // schedule next
            nextAttackTime = Time.time + Mathf.Max(attackIntervalMin, attackInterval);

            // small yield to let frame breathe
            yield return null;
        }
    }

    public void SpawnProjectileTowards(Vector3 targetWorldPos)
    {
        // Prevent spawning if enemy was stopped / dead or prefab is missing
        if (isDead || isStopped) return;
        if (projectilePrefab == null || firePoint == null) return;

        GameObject p = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        ProjectileMover mover = p.GetComponent<ProjectileMover>();
        if (mover != null)
        {
            // initialize mover (mover.Initialize will set its own lifetime)
            mover.Initialize(targetWorldPos, mover.speed, playerController);
        }
    }

    /// <summary>
    /// Decrease attack interval (increase aggression). Clamped to attackIntervalMin.
    /// </summary>
    public void IncreaseAggression(float reduceBySeconds)
    {
        attackInterval = Mathf.Max(attackIntervalMin, attackInterval - Mathf.Abs(reduceBySeconds));
    }

    /// <summary>
    /// Stop attacking immediately without playing death animation.
    /// Useful to call when level completes and you just want the enemy to stop.
    /// </summary>
    public void StopAttacking()
    {
        if (isStopped) return;
        isStopped = true;

        // Stop the attack coroutine if running
        if (attackCoroutineRef != null)
        {
            StopCoroutine(attackCoroutineRef);
            attackCoroutineRef = null;
        }

        // prevent future spawns
        projectilePrefab = null;
        nextAttackTime = float.MaxValue;
    }

    /// <summary>
    /// Public death method: stop attacks, disable collisions, play death anim.
    /// </summary>
    public void Die()
    {
        if (isDead && ClickMoveXWithSpine.Instance.isDead) return;
        isDead = true;

        // ensure we stop attacking
        StopAttacking();

        // Play death animation (Animator or Spine)
        if (animator != null)
        {
            animator.SetTrigger(deathTriggerName);
        }
        else if (spineSkeleton != null)
        {
            spineSkeleton.AnimationState.SetAnimation(0, spineDeathAnim, false);
        }

        // Disable colliders so enemy is inert
        var col2d = GetComponent<Collider2D>();
        if (col2d != null) col2d.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Optionally destroy after delay
        if (deathDestroyDelay > 0f)
        {
            Destroy(gameObject, deathDestroyDelay);
        }
    }

    // Alias to play death without destroying (if you want to keep object)
    public void TriggerDeath()
    {
        Die();
    }

    void OnDisable()
    {
        // safety: stop coroutine if component disabled
        if (attackCoroutineRef != null)
        {
            try { StopCoroutine(attackCoroutineRef); } catch { }
            attackCoroutineRef = null;
        }
    }
}
