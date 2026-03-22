using UnityEngine;

public class HandSetup : MonoBehaviour
{
    public GameObject leftHand;
    public GameObject rightHand;

    void Start()
    {
        if (GameSessionSettings.Instance == null)
        {
            Debug.LogError("GameSessionSettings missing.");
            return;
        }

        var selected = GameSessionSettings.Instance.selectedHand;

        switch (selected)
        {
            case HandType.Left:
                if (leftHand != null) leftHand.SetActive(true);
                if (rightHand != null) rightHand.SetActive(false);
                break;

            case HandType.Right:
                if (leftHand != null) leftHand.SetActive(false);
                if (rightHand != null) rightHand.SetActive(true);
                break;

            case HandType.Both:
                if (leftHand != null) leftHand.SetActive(true);
                if (rightHand != null) rightHand.SetActive(true);
                break;
        }
    }
}
