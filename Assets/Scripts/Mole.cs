using UnityEngine;
using System.Collections;

public abstract class Mole : MonoBehaviour
{
    protected Hole hole;
    protected SpawnManager manager;
    protected DifficultyData difficulty;
    protected GameObject originalPrefab;
    protected bool isAlive;

    public GameObject floatingTextPrefab;

   public void Init(Hole owningHole, SpawnManager spawnManager, DifficultyData diff, GameObject prefab)
{
    hole           = owningHole;
    manager        = spawnManager;
    difficulty     = diff;
    originalPrefab = prefab;
    isAlive        = true;

    transform.position = hole.BottomPosition;

    // Always face the player (camera)
    Transform cam = Camera.main != null ? Camera.main.transform : null;
    if (cam != null)
    {
        Vector3 direction = cam.position - transform.position;
        direction.y = 0f; // keep upright — only rotate on Y axis
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    StartCoroutine(LifeCycle());
}

    IEnumerator LifeCycle()
    {
        // Rise to top
        yield return Move(hole.BottomPosition, hole.TopPosition);

        // Calculate rise time from moleSpeed — same formula as Move() coroutine:
        // t increments by (Time.deltaTime * moleSpeed) each frame until t >= 1
        // So riseTime ≈ 1.0 / moleSpeed
        float riseTime = 1.0f / Mathf.Max(difficulty.moleSpeed, 0.01f);

        // Small fixed buffer so sound plays just after fully risen
        float soundBuffer = 0.1f;

        // Play notification sound
        yield return new WaitForSeconds(soundBuffer);
        if (isAlive) PlayNotificationSound();

        // Wait remaining lifetime (total lifetime minus rise time minus buffer)
        float remainingLifetime = Mathf.Max(0f, difficulty.moleLifetime - riseTime - soundBuffer);
        yield return new WaitForSeconds(remainingLifetime);

        if (isAlive) Die();
    }

    /// <summary>
    /// Override in GoodMole / BadMole to play the correct sound.
    /// </summary>
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
        if (!other.CompareTag("LeftHand") && !other.CompareTag("RightHand"))
            return;

        Debug.Log("Mole triggered by: " + other.name);

        if (!isAlive)
        {
            Debug.Log("Mole not alive.");
            return;
        }

        if (GameSessionSettings.Instance == null)
        {
            Debug.Log("GameSessionSettings is NULL.");
            return;
        }

        var settings = GameSessionSettings.Instance;

        Debug.Log("Movement Detection Enabled: " + settings.movementDetectionEnabled);
        Debug.Log("Selected Hand: " + settings.selectedHand);

        bool validHand = false;

        if (other.CompareTag("LeftHand"))
        {
            Debug.Log("Hit by LEFT hand");
            if (settings.selectedHand == HandType.Left || settings.selectedHand == HandType.Both)
                validHand = true;
        }
        else if (other.CompareTag("RightHand"))
        {
            Debug.Log("Hit by RIGHT hand");
            if (settings.selectedHand == HandType.Right || settings.selectedHand == HandType.Both)
                validHand = true;
        }
        else
        {
            Debug.Log("Collider is NOT tagged as LeftHand or RightHand");
        }

        if (!validHand)
        {
            Debug.Log("Hand not allowed by settings.");
            return;
        }

        var detector =
            other.GetComponent<ElbowAngleGestureDetector>() ??
            other.GetComponentInParent<ElbowAngleGestureDetector>();

        if (settings.movementDetectionEnabled)
        {
            Debug.Log("Movement detection REQUIRED");

            if (detector == null)
            {
                Debug.Log("NO ElbowAngleGestureDetector found!");
                return;
            }

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
            Debug.Log("Movement detection NOT required.");
        }

        Debug.Log("HIT SUCCESSFUL");
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