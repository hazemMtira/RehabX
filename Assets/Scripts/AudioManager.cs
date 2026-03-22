using UnityEngine;

/// <summary>
/// Manages all game audio:
///   - Background music (loops during round)
///   - Good mole notification sound (spatial — plays from mole position)
///   - Bad mole notification sound (spatial — plays from mole position)
///
/// SETUP:
///   1. Create an empty GameObject in your game scene named "AudioManager"
///   2. Attach this script
///   3. Assign audio clips in the inspector
///   4. RoundController calls StartMusic() / StopMusic()
///   5. Mole.cs calls PlayGoodMoleSound() / PlayBadMoleSound() with world position
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [Tooltip("Background music track — loops during the round")]
    public AudioClip musicTrack;

    [Tooltip("Music volume")]
    [Range(0f, 1f)]
    public float musicVolume = 0.6f;

    [Header("Mole Sounds")]
    [Tooltip("Sound played after a good mole fully rises — plays from mole position")]
    public AudioClip goodMoleSound;

    [Tooltip("Sound played after a bad mole fully rises — plays from mole position")]
    public AudioClip badMoleSound;

    [Tooltip("Volume for mole notification sounds")]
    [Range(0f, 1f)]
    public float moleVolume = 0.8f;

    // ---------------------------------------------------------------
    // Private
    // ---------------------------------------------------------------

    AudioSource _musicSource;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Music source — 2D, loops
        _musicSource              = gameObject.AddComponent<AudioSource>();
        _musicSource.clip         = musicTrack;
        _musicSource.loop         = true;
        _musicSource.volume       = musicVolume;
        _musicSource.playOnAwake  = false;
        _musicSource.spatialBlend = 0f; // 2D — music fills both ears equally
    }
    void Update()
    {
    // Keep music volume in sync with inspector slider at runtime
    if (_musicSource != null)
        _musicSource.volume = musicVolume;
    }

    // ---------------------------------------------------------------
    // Music control — called by RoundController
    // ---------------------------------------------------------------

    public void StartMusic()
    {
        if (musicTrack == null)
        {
            Debug.LogWarning("[AudioManager] No music track assigned.");
            return;
        }
        _musicSource.clip = musicTrack;
        _musicSource.Play();
        Debug.Log("[AudioManager] Music started.");
    }

    public void StopMusic()
    {
        _musicSource.Stop();
        Debug.Log("[AudioManager] Music stopped.");
    }

    // ---------------------------------------------------------------
    // Mole sounds — spatial, called by GoodMole / BadMole
    // ---------------------------------------------------------------

    /// <summary>
    /// Plays the good mole sound at the mole's world position (3D spatial).
    /// </summary>
    public void PlayGoodMoleSound(Vector3 position)
    {
        if (goodMoleSound == null) return;
        AudioSource.PlayClipAtPoint(goodMoleSound, position, moleVolume);
    }

    /// <summary>
    /// Plays the bad mole sound at the mole's world position (3D spatial).
    /// </summary>
    public void PlayBadMoleSound(Vector3 position)
    {
        if (badMoleSound == null) return;
        AudioSource.PlayClipAtPoint(badMoleSound, position, moleVolume);
    }
}
