using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

public class VolumeSettings : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    public void SetMasterVolume(float volume)
    {
        SetVolume("MasterVolume", volume);
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
