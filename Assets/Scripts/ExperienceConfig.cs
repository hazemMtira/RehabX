using UnityEngine;

/// <summary>
/// Describes this VR experience to the dashboard:
/// what body part it targets, and whether the clinician
/// must choose a hand and/or finger before launching.
///
/// Create via: Assets → Create → Game → Experience Config
/// Assign to GameFlowController.experienceConfig
/// </summary>
[CreateAssetMenu(menuName = "Game/Experience Config", fileName = "ExperienceConfig")]
public class ExperienceConfig : ScriptableObject
{
    // ── Identity ──────────────────────────────────────────────────────────
    [Tooltip("Display name sent to the dashboard (leave blank to use Application.productName)")]
    public string experienceName = "";

    [Tooltip("Single emoji shown on the dashboard card")]
    public string icon = "🎮";

    [Tooltip("Short description shown on the dashboard card")]
    [TextArea(2, 4)]
    public string description = "";

    // ── Category ──────────────────────────────────────────────────────────
    public enum ExperienceCategory
    {
        UpperLimb,
        LowerLimb,
        Balance,
        Cognitive,
        Bilateral,
        Respiratory,
        FingerDexterity,
        Other
    }

    [Tooltip("Rehabilitation category shown on the dashboard")]
    public ExperienceCategory category = ExperienceCategory.UpperLimb;

    // ── Interaction requirements ───────────────────────────────────────────
    public enum HandMode
    {
        /// <summary>No hand selection — bilateral or body-only experience</summary>
        None,
        /// <summary>Clinician picks Left, Right, or Both before launching</summary>
        Single
    }

    public enum FingerMode
    {
        /// <summary>No finger selection required</summary>
        None,
        /// <summary>Clinician picks a specific finger before launching</summary>
        All
    }

    [Header("Interaction Requirements")]
    [Tooltip("None = no hand selection needed. Single = clinician picks left/right/both.")]
    public HandMode handMode = HandMode.Single;

    [Tooltip("None = no finger selection needed. All = clinician picks a finger.")]
    public FingerMode fingerMode = FingerMode.All;

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Serialized string expected by the dashboard ("none" | "single").</summary>
    public string HandModeKey   => handMode   == HandMode.None   ? "none" : "single";

    /// <summary>Serialized string expected by the dashboard ("none" | "all").</summary>
    public string FingerModeKey => fingerMode == FingerMode.None ? "none" : "all";

    /// <summary>Serialized category string sent to Supabase.</summary>
    public string CategoryKey
    {
        get
        {
            switch (category)
            {
                case ExperienceCategory.UpperLimb:       return "Upper Limb";
                case ExperienceCategory.LowerLimb:       return "Lower Limb";
                case ExperienceCategory.Balance:         return "Balance";
                case ExperienceCategory.Cognitive:       return "Cognitive";
                case ExperienceCategory.Bilateral:       return "Bilateral";
                case ExperienceCategory.Respiratory:     return "Respiratory";
                case ExperienceCategory.FingerDexterity: return "Finger Dexterity";
                default:                                  return "Other";
            }
        }
    }
}