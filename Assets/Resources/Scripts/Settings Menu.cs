using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    public AudioMixer audioMixer;
    public TMP_Dropdown resolutionDropdown;
    public Image MainMenuCardColor;

    private int currentDisplayModeIndex = 0;

    Resolution[] resolutions;
    void Start()
    {
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        List<string> resolutionOptions = new();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string resolutionOption = resolutions[i].width + " X " + resolutions[i].height;
            resolutionOptions.Add(resolutionOption);

            if (resolutions[i].width == Screen.currentResolution.width && resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }
        resolutionDropdown.AddOptions(resolutionOptions);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }
    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        if (currentDisplayModeIndex == 0)
        {
            Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.FullScreenWindow);
        }
        else if (currentDisplayModeIndex == 1)
        {
            Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.ExclusiveFullScreen);
        }
        else if (resolutionIndex == 2)
        {
            Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.Windowed);
        }
    }
    public void SetVolume(float volume)
    {
		audioMixer.SetFloat("volume", volume);
    }
    public void SetDisplayMode(int modeIndex)
    {
        if (modeIndex == 0)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }
        else if (modeIndex == 1)
        {
            Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
        }
        else if (modeIndex == 2)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }
        currentDisplayModeIndex = modeIndex;
    }
    public void CloseButton()
    {
        GameObject button = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        button.transform.parent.parent.gameObject.SetActive(false);
        MainMenuCardColor.color = new Color32(0, 0, 0, 255); //Black
    }
}
