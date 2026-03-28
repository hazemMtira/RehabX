using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGameMenuController : MonoBehaviour
{
    /// <summary>
    /// Returns to the loading scene so the therapist can start a new session
    /// from the dashboard. SupabaseManager resets and polls automatically.
    /// </summary>
    public void PlayAgain()
    {
        if (SupabaseManager.Instance != null)
            SupabaseManager.Instance.ResetForNewSession();
        else
            SceneManager.LoadScene("LoadingScene");
    }

    /// <summary>
    /// Closes the application.
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}