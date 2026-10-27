using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;

    private static AudioSource audioSource;
    private static SoundLibrary soundEffectLibrary;
    private static AudioSource randomPitchAudioSource;
    [SerializeField]private Slider sfxSlider;
    public void Awake()
    {
        if(instance == null)
        {
            instance = this;
            AudioSource[] audioSources = GetComponents<AudioSource>();
            audioSource = audioSources[0];
            randomPitchAudioSource = audioSources[1];
            audioSource = GetComponent<AudioSource>();
            soundEffectLibrary = GetComponent<SoundLibrary>();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        sfxSlider.onValueChanged.AddListener(delegate { OnValueChange(); });
    }
    public static void Play(string soundName,bool randomPitch = false)
    {
        AudioClip audioClip = soundEffectLibrary.GetRandomClip(soundName);
        if(audioClip != null)
        {
            if (randomPitch)
            {
                randomPitchAudioSource.pitch = Random.Range(1f, 1.5f);
                randomPitchAudioSource.PlayOneShot(audioClip);
            }
            else
            {
                audioSource.PlayOneShot(audioClip);
            }
        }
    }
    public static void SetVolume(float volume)
    {
        audioSource.volume = volume;
        randomPitchAudioSource.volume = volume;
    }
    public void OnValueChange()
    {
        SetVolume(sfxSlider.value);
    }
}
