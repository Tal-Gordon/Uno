using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject connectionMenu;
    public GameObject hostMenu;
    public GameObject joinMenu;

    public Image MainMenuCardColor;
    public Image redButton;
    public Image yellowButton;
    public Image blueButton;
    public Image greenButton;
    public Image hostButton;
    public Image joinButton;

    public string gameSceneName;
    public string commTestSceneName;

    private void Start()
    {
        MainMenuCardColor.color = new Color32(0, 0, 0, 255); //Black
        redButton.alphaHitTestMinimumThreshold = 1;
        yellowButton.alphaHitTestMinimumThreshold = 1;
        blueButton.alphaHitTestMinimumThreshold = 1;
        greenButton.alphaHitTestMinimumThreshold = 1;
        hostButton.alphaHitTestMinimumThreshold = 1;
        joinButton.alphaHitTestMinimumThreshold = 1;

        Application.targetFrameRate = 60;
    }
    public void Singleplayer() 
    {
        MainMenuCardColor.color = new Color32(234, 201, 0, 255); //Yellow
        SceneManager.LoadScene(gameSceneName);
    }
    public void Multiplayer()
    {
        connectionMenu.SetActive(true);
        MainMenuCardColor.color = new Color32(219, 29, 30, 255); //Red
    }
    public void Settings()
    {
        MainMenuCardColor.color = new Color32(2, 95, 168, 255); //Blue
    }
    public void Exit()
    {
        MainMenuCardColor.color = new Color32(53, 147, 61, 255); //Green
        Application.Quit();
    }
    public void CommTest()
    {
        SceneManager.LoadScene(commTestSceneName);
    }
    public void Join()
    {
        joinMenu.SetActive(true);
    }
    public void Host()
    {
        hostMenu.SetActive(true);
    }

    public void CloseButton()
    {
        GameObject button = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        button.transform.parent.gameObject.SetActive(false);
    }
    public void BackArrow()
    {
        CloseButton();
        MainMenuCardColor.color = new Color32(0, 0, 0, 255); //Black
    }
}
