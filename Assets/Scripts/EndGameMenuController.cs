using UnityEngine;

public class EndGameMenuController : MonoBehaviour
{
    public void RestartGame()
    {
        if (GameManager.Instance != null) GameManager.Instance.RestartGame();
    }

    public void LoadMainMenu()
    {
        if (GameManager.Instance != null) GameManager.Instance.LoadMainMenu();
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