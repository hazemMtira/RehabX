using UnityEngine;

public class Hole : MonoBehaviour
{
    public Transform topPoint;
    public Transform bottomPoint;

    private Mole currentMole;

    public Vector3 TopPosition => topPoint.position;
    public Vector3 BottomPosition => bottomPoint.position;

    public bool IsFree => currentMole == null;

    public void SetMole(Mole mole)
    {
        if (currentMole != null)
        {
            Debug.LogWarning("Hole already occupied!");
            return;
        }

        currentMole = mole;
    }

    public void ClearHole()
    {
        currentMole = null;
    }
}
