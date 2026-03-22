using UnityEngine;

public class EndGameMenuController : MonoBehaviour
{
    /// <summary>
    /// Sends a restart request to Supabase so the therapist
    /// sees a notification on the dashboard.
    /// </summary>
    public void RequestRestart()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RequestRestart();
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