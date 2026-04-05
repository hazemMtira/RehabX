using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGameMenuController : MonoBehaviour
{
    /// <summary>
    /// Resets the bridge and waits for the therapist to start
    /// a new session from the dashboard.
    /// </summary>
    public void PlayAgain()
    {
        if (SupabaseGameBridge.Instance != null)
        {
            SupabaseGameBridge.Instance.ResetForNewSession();
        }
        else
        {
            Debug.LogWarning("[EndGameMenu] No SupabaseGameBridge found.");
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}