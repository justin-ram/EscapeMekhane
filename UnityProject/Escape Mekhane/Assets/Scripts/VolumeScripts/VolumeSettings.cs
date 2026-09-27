using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class VolumeSettings : MonoBehaviour
{
    public Slider volumeSlider;
    void Start()
    {
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
       
        volumeSlider.value = savedVolume;
        
        
        
    }
    [SerializeField] private AudioMixer audioMixer;
    public void SetMasterVolume(float volume)
    {
        SetVolume("MasterVolume", volume);
        PlayerPrefs.SetFloat("Master", volume);
        PlayerPrefs.Save();
    }
    public void SetMusicVolume(float volume)
    {
        SetVolume("MusicVolume", volume);
    }
    public void SetEnvironmentVolume(float volume)
    {
        SetVolume("EnvironmentVolume", volume);
    }
    private void SetVolume(string parameterName, float volume)
    {
        float decibels = Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;
        audioMixer.SetFloat(parameterName, decibels);
    }
}
