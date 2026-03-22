using UnityEngine;

public class GoodMole : Mole
{
    protected override void PlayNotificationSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGoodMoleSound(transform.position);
    }

    protected override void OnHit()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(1);
        SpawnFloatingText("+1", Color.green);
    }
}