using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioClip musicClip;   // 🎵 Assign your background music file in the Inspector
    public float volume = 0.5f;   // Volume (0.0 – 1.0)

    private AudioSource audioSource;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = musicClip;
        audioSource.volume = volume;
        audioSource.loop = true;   // 🎯 Loop the music
        audioSource.playOnAwake = true;
        audioSource.Play();
    }

    // 🔊 Optional: Allow volume changes at runtime
    public void SetVolume(float newVolume)
    {
        audioSource.volume = Mathf.Clamp01(newVolume);
    }
}
