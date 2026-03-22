using UnityEngine;

public class BadMole : Mole
{
    protected override void PlayNotificationSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBadMoleSound(transform.position);
    }

    protected override void OnHit()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(-1);
        SpawnFloatingText("-1", Color.red);
    }
}