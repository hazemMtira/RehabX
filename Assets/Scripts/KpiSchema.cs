using UnityEngine;

/// <summary>
/// Defines one KPI metric that this VR experience reports back to the dashboard
/// after a session ends (e.g. score, angle, accuracy, range_of_motion).
///
/// Create via: Assets → Create → Game → KPI Schema
/// Assign the array to GameFlowController.kpiSchema[].
/// </summary>
[CreateAssetMenu(menuName = "Game/KPI Schema", fileName = "KPI_score")]
public class KpiSchema : ScriptableObject
{
    public enum KPIType
    {
        Int,        // whole number  — e.g. score, reps
        Float,      // decimal       — e.g. velocity, force
        Percentage, // 0-100 %       — e.g. accuracy
        Angle,      // degrees       — e.g. ROM, max flexion
        Duration,   // seconds       — e.g. hold time
        Text        // free string   — e.g. dominant hand used
    }

    [Header("Identity")]
    [Tooltip("camelCase key — must match the key used in IKPIProvider.GetKPIResults().")]
    public string key   = "score";
    public string label = "Score";
    public string unit  = "pts";

    [Header("Type — controls dashboard formatting")]
    public KPIType type = KPIType.Int;

    [Tooltip("Used by the dashboard to colour-code the value (green = better / red = worse).")]
    public bool higherIsBetter = true;
}