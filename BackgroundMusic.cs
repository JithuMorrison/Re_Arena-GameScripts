using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioClip[] musicClips;   // 🎵 Assign multiple clips in the Inspector
    public float volume = 0.5f;      // Volume (0.0 – 1.0)

    private AudioSource audioSource;
    private int currentClipIndex = 0;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.volume = volume;
        audioSource.loop = false;   // 🚫 Don't loop single clip, we'll handle sequence
        audioSource.playOnAwake = false;

        if (musicClips.Length > 0)
        {
            StartCoroutine(PlayMusicLoop());
        }
    }

    private IEnumerator PlayMusicLoop()
    {
        while (true) // infinite loop
        {
            if (musicClips.Length == 0) yield break;

            audioSource.clip = musicClips[currentClipIndex];
            audioSource.Play();

            // Wait until the clip finishes
            yield return new WaitForSeconds(audioSource.clip.length);

            // Move to next clip (loop back when reaching end)
            currentClipIndex = (currentClipIndex + 1) % musicClips.Length;
        }
    }

    // 🔊 Optional: Allow volume changes at runtime
    public void SetVolume(float newVolume)
    {
        audioSource.volume = Mathf.Clamp01(newVolume);
    }
}
