using UnityEngine;
using TMPro;
using UnityEngine.XR;
using System.Collections.Generic;

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
    [Tooltip("Elbow angle (deg) below which the arm is considered bent/cocked. " +
             "Your resting angle is ~160° so this must be below that to require actual bending.")]
    public float flexionThreshold = 145f;

    [Header("Strike Phase")]
    [Tooltip("Minimum downward wrist speed (m/s) to register as a valid strike.")]
    public float minStrikeDownwardSpeed = 0.4f;

    [Tooltip("How long (seconds) gestureReady stays true after a strike.")]
    public float gestureReadyDuration = 1f;

    [Tooltip("Maximum physically possible wrist speed (m/s). Clamps tracking spikes.")]
    public float maxPhysicalSpeed = 6f;

    [Header("Debug Overlay")]
    public bool showDebugOverlay = true;
    public Canvas debugCanvas;
    public TextMeshProUGUI debugText;
    public bool showDebugLogs = false;

    // ---------------------------------------------------------------
    // Public state (read by Mole.cs / RoundController)
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
    float   _maxWristSpeed;       // peak raw speed — reset each round
    bool    _wasTrackingLost;     // flag to skip one frame after tracking recovers

    float _lastAngle;
    float _angularVelocity;
    bool  _initialized;

    bool  _armCocked;
    float _lastStrikeTime = -999f;

    // ---------------------------------------------------------------
    // Calibration (kept for future use)
    // ---------------------------------------------------------------

    public bool calibrationMode = false;
    float _calMinAngle =  999f;
    float _calMaxAngle =    0f;
    float _calMaxSpeed =    0f;

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    public float GetWristSpeed()    => _wristVerticalSpeed;
    public float GetMaxWristSpeed() => _maxWristSpeed;
    public void  ResetMaxWristSpeed() { _maxWristSpeed = 0f; }

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
        // ── Tracking confidence check ──────────────────────────────
        if (!IsHandTrackingConfident())
        {
            _lastWristPos    = hand.position; // prevent spike on recovery
            _wasTrackingLost = true;
            return;
        }

        // Skip one frame after tracking recovers — position may have jumped
        if (_wasTrackingLost)
        {
            _lastWristPos    = hand.position;
            _wasTrackingLost = false;
            return;
        }

        // ── Geometry ───────────────────────────────────────────────
        ComputeSegmentLengths();
        float angle = CalculateElbowAngle();

        if (!_initialized)
        {
            _lastAngle    = angle;
            _lastWristPos = hand.position;
            _initialized  = true;
            return;
        }

        // ── Angular velocity (smoothed) ────────────────────────────
        float rawVelocity = (angle - _lastAngle) / Time.deltaTime;
        _angularVelocity  = Mathf.Lerp(_angularVelocity, rawVelocity, 0.8f);

        // ── Wrist vertical speed ───────────────────────────────────
        float rawVertical = (hand.position.y - _lastWristPos.y) / Time.deltaTime;

        // Clamp to physically possible range — kills any remaining tracking spikes
        rawVertical = Mathf.Clamp(rawVertical, -maxPhysicalSpeed, maxPhysicalSpeed);

        _wristVerticalSpeed = Mathf.Lerp(_wristVerticalSpeed, rawVertical, 0.9f);

        // Deadzone — kill jitter
        if (Mathf.Abs(_wristVerticalSpeed) < 0.08f)
            _wristVerticalSpeed = 0f;

        // Track peak raw speed — only valid clamped values recorded here
        float absClamped = Mathf.Abs(rawVertical);
        if (absClamped > _maxWristSpeed)
            _maxWristSpeed = absClamped;

        // ── State machine ──────────────────────────────────────────
        if (calibrationMode)
            RecordCalibration(angle);
        else
            UpdateStrikeStateMachine(angle);

        _lastAngle    = angle;
        _lastWristPos = hand.position;

        // ── Debug ──────────────────────────────────────────────────
        if (showDebugOverlay)
            UpdateOverlay(angle);

        if (showDebugLogs)
            Debug.Log($"[GestureDetector] angle={angle:F1}° vel={_angularVelocity:F1}°/s " +
                      $"wristV={_wristVerticalSpeed:F2}m/s maxV={_maxWristSpeed:F2}m/s " +
                      $"cocked={_armCocked} ready={gestureReady}");
    }

    // ---------------------------------------------------------------
    // Tracking confidence check
    // ---------------------------------------------------------------

    bool IsHandTrackingConfident()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.HandTracking, devices);

        // No hand tracking devices = editor mode, assume confident
        if (devices.Count == 0) return true;

        foreach (var device in devices)
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && !tracked)
                return false;

        return true;
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
    // Strike state machine
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

        // Phase 1: Cock — arm must bend below flexionThreshold
        if (!_armCocked)
        {
            if (angle < flexionThreshold)
            {
                _armCocked = true;
                if (showDebugLogs)
                    Debug.Log($"[GestureDetector] 🔼 ARM COCKED  angle={angle:F1}°");
            }
            return;
        }

        // Phase 2: Strike — wrist moving down fast enough
        if (_wristVerticalSpeed < -minStrikeDownwardSpeed)
        {
            gestureReady    = true;
            _armCocked      = false;
            _lastStrikeTime = Time.time;

            if (showDebugLogs)
                Debug.Log($"[GestureDetector] ✅ STRIKE  downSpeed={-_wristVerticalSpeed:F2}m/s");
        }
    }

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
    // Debug overlay
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
        bool   confident   = IsHandTrackingConfident();

        float timeLeft = gestureReady
            ? Mathf.Max(0f, gestureReadyDuration - (Time.time - _lastStrikeTime))
            : 0f;

        debugText.text =
            $"<color=#FFDD44>Elbow  </color> {angle:F1}°\n" +
            $"<color=#FFDD44>WristV </color> {dirLabel} {Mathf.Abs(_wristVerticalSpeed):F2}m/s\n" +
            $"<color=#FFDD44>MaxV   </color> {_maxWristSpeed:F2}m/s\n" +
            $"<color=#FFDD44>Cocked </color> <color={cockedColor}>{_armCocked}</color>\n" +
            $"<color=#FFDD44>READY  </color> <color={readyColor}>{gestureReady}</color>" +
            (gestureReady ? $" <size=70%>{timeLeft:F2}s</size>" : "") + "\n" +
            $"<color=#FFDD44>Track  </color> <color={(confident ? "#00FF88" : "#FF4444")}>{(confident ? "OK" : "LOST")}</color>\n" +
            $"<size=60%>" +
            $"flex<{flexionThreshold:F0}°  " +
            $"strike↓>{minStrikeDownwardSpeed:F1}m/s  " +
            $"window={gestureReadyDuration:F2}s  " +
            $"cap={maxPhysicalSpeed:F0}m/s</size>";
    }
}