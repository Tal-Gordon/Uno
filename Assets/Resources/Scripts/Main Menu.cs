using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public string gameSceneName;
    public string commTestSceneName;

    public Image redButton;
    public Image yellowButton;
    public Image blueButton;
    public Image greenButton;

    private void Start()
    {
        redButton.alphaHitTestMinimumThreshold = 1;
        yellowButton.alphaHitTestMinimumThreshold = 1;
        blueButton.alphaHitTestMinimumThreshold = 1;
        greenButton.alphaHitTestMinimumThreshold = 1;
    }
    public void Play() 
    {
        SceneManager.LoadScene(gameSceneName);
    }
    public void Settings()
    {

    }
    public void Exit()
    {
        Application.Quit();
    }

    public void CommTest()
    {
        SceneManager.LoadScene(commTestSceneName);
    }
}
