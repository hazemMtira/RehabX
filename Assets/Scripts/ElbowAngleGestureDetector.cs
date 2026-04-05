using UnityEngine;
using TMPro;

public class ElbowAngleGestureDetector : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Inspector
    // ---------------------------------------------------------------

    [Header("References")]
    [Tooltip("Assign Main Camera transform. Auto-found if left empty.")]
    public Transform xrCamera;

    [Tooltip("The wrist bone that moves with hand tracking (e.g. L_Wrist / R_Wrist).")]
    public Transform hand;

    [Header("Cocking Phase")]
    [Tooltip("Elbow angle (deg) below which the arm is considered bent/cocked.")]
    public float flexionThreshold = 150f;

    [Tooltip("Minimum upward wrist speed (m/s) while bent to confirm the arm is being raised.")]
    public float minCockUpwardSpeed = 0.10f;

    [Header("Strike Phase")]
    [Tooltip("Minimum downward wrist speed (m/s) to register as a valid strike.")]
    public float minStrikeDownwardSpeed = 0.6f;

    [Tooltip("How long (seconds) gestureReady stays true after a strike.")]
    public float gestureReadyDuration = 0.2f;

    [Header("Debug Overlay")]
    public bool showDebugOverlay = true;
    public Canvas debugCanvas;
    public TextMeshProUGUI debugText;
    public bool showDebugLogs = false;

    // ---------------------------------------------------------------
    // Public state (read by Mole.cs)
    // ---------------------------------------------------------------

    public bool gestureReady { get; private set; }

    // ---------------------------------------------------------------
    // Private state
    // ---------------------------------------------------------------

    float _l1, _l2;
    float _shoulderLateralOffset;
    float _shoulderDropFromEyes;

    Vector3 _lastWristPos;
    float   _wristVerticalSpeed;

    float _lastAngle;
    float _angularVelocity;
    bool  _initialized;

    bool  _armCocked;
    float _lastStrikeTime = -999f;

    // ── KPI tracking ─────────────────────────────────────────────
    float _maxWristSpeed = 0f;

    // ---------------------------------------------------------------
    // Calibration (kept for future use)
    // ---------------------------------------------------------------

    public bool calibrationMode = false;
    float _calMinAngle =  999f;
    float _calMaxAngle =    0f;
    float _calMaxSpeed =    0f;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    void Start()
    {
        if (xrCamera == null)
            xrCamera = Camera.main != null ? Camera.main.transform : null;

        if (hand == null)
            hand = transform;

        if (xrCamera == null)
        {
            Debug.LogError("[ElbowAngleGestureDetector] No XR Camera found. Detector disabled.");
            enabled = false;
            return;
        }

        SetOverlayVisible(showDebugOverlay);
    }

    void Update()
    {
        // Recomputed every frame so head height is always current
        ComputeSegmentLengths();

        float angle = CalculateElbowAngle();

        if (!_initialized)
        {
            _lastAngle    = angle;
            _lastWristPos = hand.position;
            _initialized  = true;
            return;
        }

        // Angular velocity (smoothed)
        float rawVelocity = (angle - _lastAngle) / Time.deltaTime;
        _angularVelocity  = Mathf.Lerp(_angularVelocity, rawVelocity, 0.8f);

        // Wrist vertical speed — positive up, negative down
        float rawVertical   = (hand.position.y - _lastWristPos.y) / Time.deltaTime;
        _wristVerticalSpeed = Mathf.Lerp(_wristVerticalSpeed, rawVertical, 0.8f);

        // Deadzone — kill jitter
        if (Mathf.Abs(_wristVerticalSpeed) < 0.15f)
            _wristVerticalSpeed = 0f;

        // Track peak downward speed for KPI
        float downwardSpeed = -_wristVerticalSpeed;
        if (downwardSpeed > _maxWristSpeed)
            _maxWristSpeed = downwardSpeed;

        if (calibrationMode)
            RecordCalibration(angle);
        else
            UpdateStrikeStateMachine(angle);

        _lastAngle    = angle;
        _lastWristPos = hand.position;

        if (showDebugOverlay)
            UpdateOverlay(angle);

        if (showDebugLogs)
            Debug.Log($"[GestureDetector] angle={angle:F1}° vel={_angularVelocity:F1}°/s " +
                      $"wristV={_wristVerticalSpeed:F2}m/s cocked={_armCocked} ready={gestureReady}");
    }

    // ---------------------------------------------------------------
    // Geometry — recomputed every frame
    // ---------------------------------------------------------------

    void ComputeSegmentLengths()
    {
        float xrOriginY      = xrCamera.root.position.y;
        float eyeHeight      = xrCamera.position.y - xrOriginY;
        float standingHeight = eyeHeight / 0.936f;

        _l1 = standingHeight * 0.186f;
        _l2 = standingHeight * 0.146f;

        _shoulderLateralOffset = standingHeight * 0.130f;
        _shoulderDropFromEyes  = standingHeight * 0.130f;
    }

    float CalculateElbowAngle()
    {
        float lateralSign = (hand.position.x < xrCamera.position.x) ? -1f : 1f;

        Vector3 shoulderPos = xrCamera.position
                            - xrCamera.up    * _shoulderDropFromEyes
                            + xrCamera.right * (_shoulderLateralOffset * lateralSign);

        float d    = Vector3.Distance(shoulderPos, hand.position);
        float minD = Mathf.Abs(_l1 - _l2) + 0.001f;
        float maxD = _l1 + _l2 - 0.001f;
        d = Mathf.Clamp(d, minD, maxD);

        float cosAngle = (_l1 * _l1 + _l2 * _l2 - d * d) / (2f * _l1 * _l2);
        cosAngle = Mathf.Clamp(cosAngle, -1f, 1f);
        return Mathf.Acos(cosAngle) * Mathf.Rad2Deg;
    }

    // ---------------------------------------------------------------
    // Strike state machine — unchanged from working version
    // ---------------------------------------------------------------

    void UpdateStrikeStateMachine(float angle)
    {
        // Keep gestureReady alive for gestureReadyDuration seconds
        if (gestureReady)
        {
            if (Time.time - _lastStrikeTime > gestureReadyDuration)
                gestureReady = false;
            return;
        }

        // --- Phase 1: Cock ---
        if (!_armCocked)
        {
            if (angle < flexionThreshold)
            {
                _armCocked = true;
                if (showDebugLogs)
                    Debug.Log($"[GestureDetector] ARM COCKED  angle={angle:F1}°");
            }
            return;
        }

        // --- Phase 2: Strike ---
        if (_wristVerticalSpeed < -minStrikeDownwardSpeed)
        {
            gestureReady    = true;
            _armCocked      = false;
            _lastStrikeTime = Time.time;

            if (showDebugLogs)
                Debug.Log($"[GestureDetector] STRIKE  downSpeed={-_wristVerticalSpeed:F2}m/s");
        }
    }

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Returns the current smoothed wrist vertical speed.
    /// Negative = moving down, positive = moving up.
    /// </summary>
    public float GetWristSpeed()
    {
        return _wristVerticalSpeed;
    }

    /// <summary>
    /// Returns the peak downward wrist speed recorded since last reset.
    /// Used by GameFlowController as a KPI metric for the dashboard.
    /// </summary>
    public float GetMaxWristSpeed()
    {
        return _maxWristSpeed;
    }

    /// <summary>
    /// Resets the peak wrist speed tracker to zero.
    /// Called by GameFlowController at the start of each session or level.
    /// </summary>
    public void ResetMaxWristSpeed()
    {
        _maxWristSpeed = 0f;
    }

    /// <summary>
    /// Fully resets internal detector state.
    /// Called by GameFlowController when the hand is swapped or re-enabled
    /// mid-session so the detector doesn't carry stale position/angle data.
    /// </summary>
    public void ForceReinitialize()
    {
        _initialized        = false;
        _armCocked          = false;
        gestureReady        = false;
        _wristVerticalSpeed = 0f;
        _angularVelocity    = 0f;
        _lastStrikeTime     = -999f;

        if (hand != null)
            _lastWristPos = hand.position;

        if (showDebugLogs)
            Debug.Log("[GestureDetector] ForceReinitialize() called — state cleared.");
    }

    /// <summary>
    /// Called by Mole.cs after a gesture hit is consumed.
    /// </summary>
    public void ConsumeGesture()
    {
        gestureReady = false;
        _armCocked   = false;
    }

    // ---------------------------------------------------------------
    // Calibration (kept for future use)
    // ---------------------------------------------------------------

    void RecordCalibration(float angle)
    {
        if (angle < _calMinAngle) _calMinAngle = angle;
        if (angle > _calMaxAngle) _calMaxAngle = angle;
        if (_angularVelocity > _calMaxSpeed) _calMaxSpeed = _angularVelocity;
    }

    public void ApplyCalibration()
    {
        flexionThreshold = _calMinAngle + 10f;
        Debug.Log($"[GestureDetector] Calibration applied → flex<{flexionThreshold:F1}°");
    }

    // ---------------------------------------------------------------
    // Debug overlay — unchanged from working version
    // ---------------------------------------------------------------

    void SetOverlayVisible(bool visible)
    {
        if (debugCanvas != null)
            debugCanvas.gameObject.SetActive(visible);
    }

    void UpdateOverlay(float angle)
    {
        if (debugText == null) return;

        string cockedColor = _armCocked   ? "#00FF88" : "#AAAAAA";
        string readyColor  = gestureReady ? "#FF4444" : "#FFFFFF";
        string dirLabel    = _wristVerticalSpeed > 0 ? "▲" : "▼";

        float timeLeft = gestureReady
            ? Mathf.Max(0f, gestureReadyDuration - (Time.time - _lastStrikeTime))
            : 0f;

        debugText.text =
            $"<color=#FFDD44>Elbow  </color> {angle:F1}°\n" +
            $"<color=#FFDD44>WristV </color> {dirLabel} {Mathf.Abs(_wristVerticalSpeed):F2}m/s\n" +
            $"<color=#FFDD44>MaxSpd </color> {_maxWristSpeed:F2}m/s\n" +
            $"<color=#FFDD44>Cocked </color> <color={cockedColor}>{_armCocked}</color>\n" +
            $"<color=#FFDD44>READY  </color> <color={readyColor}>{gestureReady}</color>" +
            (gestureReady ? $" <size=70%>{timeLeft:F2}s</size>" : "") + "\n" +
            $"<size=60%>" +
            $"flex<{flexionThreshold:F0}°  " +
            $"strike↓>{minStrikeDownwardSpeed:F1}m/s  " +
            $"window={gestureReadyDuration:F2}s</size>";
    }
}