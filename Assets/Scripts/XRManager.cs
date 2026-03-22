using UnityEngine;

public class XRManager : MonoBehaviour
{
    [Header("XR References")]
    public GameObject gameplayXROrigin;
    public GameObject endGameXROrigin;

    public void SwapToEndGame()
    {
        if (gameplayXROrigin != null) gameplayXROrigin.SetActive(false);
        if (endGameXROrigin != null) endGameXROrigin.SetActive(true);
    }

    public void SwapToGameplay()
    {
        if (endGameXROrigin != null) endGameXROrigin.SetActive(false);
        if (gameplayXROrigin != null) gameplayXROrigin.SetActive(true);
    }
}