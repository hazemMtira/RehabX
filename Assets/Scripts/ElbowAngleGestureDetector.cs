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

    [Tooltip("How long (seconds) gestureReady stays true after a strike — gives hand time to reach the mole.")]
    public float gestureReadyDuration = 1f;

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
        // Freeze detection when hand tracking is unreliable
        // (e.g. hand goes behind the headset lenses)
        if (!IsHandTrackingConfident())
        {
            _lastWristPos = hand.position; // prevent velocity spike on re-acquisition
            return;
        }

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
        // Using 0.9f lerp so speed peaks come through faster
        float rawVertical   = (hand.position.y - _lastWristPos.y) / Time.deltaTime;
        _wristVerticalSpeed = Mathf.Lerp(_wristVerticalSpeed, rawVertical, 0.9f);

        // Deadzone — kill jitter but keep real movement
        if (Mathf.Abs(_wristVerticalSpeed) < 0.08f)
            _wristVerticalSpeed = 0f;

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
    // Tracking confidence check
    // ---------------------------------------------------------------

    bool IsHandTrackingConfident()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.HandTracking, devices);

        // If no hand tracking devices found at all, assume confident
        // (e.g. in editor without headset)
        if (devices.Count == 0) return true;

        foreach (var device in devices)
        {
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked))
                if (!tracked) return false;
        }
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

        // --- Phase 1: Cock ---
        // Arm must be meaningfully bent (below flexionThreshold)
        // Since resting is ~160°, threshold of 145° means player
        // must actually raise and bend their arm
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

        // --- Phase 2: Strike ---
        bool strikingDown = _wristVerticalSpeed < -minStrikeDownwardSpeed;
        if (strikingDown)
        {
            gestureReady    = true;
            _armCocked      = false;
            _lastStrikeTime = Time.time;

            if (showDebugLogs)
                Debug.Log($"[GestureDetector] ✅ STRIKE  downSpeed={-_wristVerticalSpeed:F2}m/s");
        }
    }

    public float GetWristSpeed()
    {
        return _wristVerticalSpeed;
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

        float timeLeft = gestureReady
            ? Mathf.Max(0f, gestureReadyDuration - (Time.time - _lastStrikeTime))
            : 0f;

        bool confident = IsHandTrackingConfident();

        debugText.text =
            $"<color=#FFDD44>Elbow  </color> {angle:F1}°\n" +
            $"<color=#FFDD44>WristV </color> {dirLabel} {Mathf.Abs(_wristVerticalSpeed):F2}m/s\n" +
            $"<color=#FFDD44>Cocked </color> <color={cockedColor}>{_armCocked}</color>\n" +
            $"<color=#FFDD44>READY  </color> <color={readyColor}>{gestureReady}</color>" +
            (gestureReady ? $" <size=70%>{timeLeft:F2}s</size>" : "") + "\n" +
            $"<color=#FFDD44>Track  </color> <color={(confident ? "#00FF88" : "#FF4444")}>{(confident ? "OK" : "LOST")}</color>\n" +
            $"<size=60%>" +
            $"flex<{flexionThreshold:F0}°  " +
            $"strike↓>{minStrikeDownwardSpeed:F1}m/s  " +
            $"window={gestureReadyDuration:F2}s</size>";
    }
}