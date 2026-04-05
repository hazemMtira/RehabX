using UnityEngine;
using System.Collections;

public abstract class Mole : MonoBehaviour
{
    protected Hole          hole;
    protected SpawnManager  manager;
    protected LevelConfig   difficulty;
    protected GameObject    originalPrefab;
    protected bool          isAlive;

    public GameObject floatingTextPrefab;

    public void Init(Hole owningHole, SpawnManager spawnManager, LevelConfig config, GameObject prefab)
    {
        hole           = owningHole;
        manager        = spawnManager;
        difficulty     = config;
        originalPrefab = prefab;
        isAlive        = true;

        transform.position = hole.BottomPosition;

        // Always face the player (camera)
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
        {
            Vector3 direction = cam.position - transform.position;
            direction.y = 0f;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        StartCoroutine(LifeCycle());
    }

    IEnumerator LifeCycle()
    {
        yield return Move(hole.BottomPosition, hole.TopPosition);

        float riseTime    = 1.0f / Mathf.Max(difficulty.moleSpeed, 0.01f);
        float soundBuffer = 0.1f;

        yield return new WaitForSeconds(soundBuffer);
        if (isAlive) PlayNotificationSound();

        float remainingLifetime = Mathf.Max(0f, difficulty.moleLifetime - riseTime - soundBuffer);
        yield return new WaitForSeconds(remainingLifetime);

        if (isAlive) Die();
    }

    protected virtual void PlayNotificationSound() { }

    protected virtual void Die()
    {
        isAlive = false;
        StartCoroutine(DieRoutine());
    }

    public void ForceReturnToPool()
    {
        StopAllCoroutines();
        isAlive = false;
        transform.position = hole.BottomPosition;
        hole.ClearHole();
        manager.MoleRemoved();
        MolePoolManager.Instance.ReturnMole(originalPrefab, this);
    }

    IEnumerator DieRoutine()
    {
        yield return Move(hole.TopPosition, hole.BottomPosition);
        hole.ClearHole();
        manager.MoleRemoved();
        MolePoolManager.Instance.ReturnMole(originalPrefab, this);
    }

    IEnumerator Move(Vector3 from, Vector3 to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * difficulty.moleSpeed;
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }

    protected abstract void OnHit();

    void OnTriggerEnter(Collider other)
    {
            Debug.Log($"Mole triggered by: {other.name} | tag: {other.tag}");

        if (!other.CompareTag("LeftHand") && !other.CompareTag("RightHand"))
            return;

        Debug.Log("Mole triggered by: " + other.name);

        if (!isAlive)
        {
            Debug.Log("Mole not alive.");
            return;
        }

        // ── Hand validation via GameFlowController ─────────────────────────
        var    gfc        = GameFlowController.Instance;
        string activeHand = gfc?.ActiveHand; // "left" | "right" | "both" | null

        bool validHand = false;

        if (other.CompareTag("LeftHand"))
            validHand = string.IsNullOrEmpty(activeHand) || activeHand == "left" || activeHand == "both";
        else if (other.CompareTag("RightHand"))
            validHand = string.IsNullOrEmpty(activeHand) || activeHand == "right" || activeHand == "both";

        if (!validHand)
        {
            Debug.Log($"Hand not allowed. Active hand: {activeHand}, triggered by: {other.tag}");
            return;
        }

        // ── Gesture detection ──────────────────────────────────────────────
        var detector =
            other.GetComponent<ElbowAngleGestureDetector>() ??
            other.GetComponentInParent<ElbowAngleGestureDetector>();

        if (detector != null)
        {
            Debug.Log("Gesture Ready: " + detector.gestureReady);

            if (!detector.gestureReady)
            {
                Debug.Log("Gesture NOT ready. Blocking hit.");
                return;
            }

            Debug.Log("Gesture consumed.");
            detector.ConsumeGesture();
        }
        else
        {
            Debug.Log("No ElbowAngleGestureDetector found — hit allowed without gesture check.");
        }

        // ── Record max strike speed on successful hit ──────────────────────
        Debug.Log("HIT SUCCESSFUL");

        if (detector != null)
        {
            float peak = detector.GetMaxWristSpeed();
            Debug.Log($"[Mole] Strike speed recorded: {peak:F2} m/s");

            // Record via RoundController
            var roundController = FindFirstObjectByType<RoundController>();
            if (roundController != null)
                roundController.RecordStrikeSpeed(peak);

            // Also push directly into GFC KPI dict as backup
            gfc?.RecordKpi("maxStrikeSpeed", peak);

            detector.ResetMaxWristSpeed();
        }

        OnHit();
        Die();
    }

    protected void SpawnFloatingText(string text, Color color)
    {
        if (floatingTextPrefab == null) return;
        GameObject go = Instantiate(floatingTextPrefab,
            transform.position + Vector3.up * 0.5f, Quaternion.identity);
        go.GetComponent<FloatingText>().Initialize(text, color);
    }
}