using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGameMenuController : MonoBehaviour
{
    /// <summary>
    /// Sends restart request to Supabase so therapist sees it on dashboard,
    /// then returns to loading scene to wait for a new session.
    /// </summary>
    public void RequestRestart()
    {
        // Notify Supabase that patient wants to restart
        if (GameManager.Instance != null)
            GameManager.Instance.RequestRestart();

        // Go back to loading scene — SupabaseManager will reset and poll for new session
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