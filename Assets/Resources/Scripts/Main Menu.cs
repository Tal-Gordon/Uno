using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string gameSceneName;
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
}
