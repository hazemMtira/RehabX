using UnityEngine;

/// <summary>
/// Attached automatically by InstructionsBuilder to each 3D mole display model.
/// Keeps the model positioned in world space above its instruction card every frame.
/// </summary>
public class MoleModelPositioner : MonoBehaviour
{
    [HideInInspector] public RectTransform targetCard;
    [HideInInspector] public float verticalOffset = 0.04f;

    void LateUpdate()
    {
        if (targetCard == null) return;

        // Get the world space center of the card
        Vector3 cardCenter = targetCard.TransformPoint(
            new Vector3(0, targetCard.rect.height * 0.18f, 0));

        transform.position = cardCenter + Vector3.up * verticalOffset;

        // Face the same direction as the canvas
        transform.rotation = targetCard.rotation *
            Quaternion.Euler(0, 180f, 0);
    }
}