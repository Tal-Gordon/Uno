using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI;
    private Server server;
    private Client client;

    private bool gameIsPaused;
    void Start()
    {
        server = Server.Instance; client = Client.Instance;
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }
    void Pause()
    {
        pauseMenuUI.SetActive(true);
        gameIsPaused = true;
    }
    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        gameIsPaused = false;
    }
    public void Exit()
    {
        if (server.active)
        {
            server.CloseServer();
        }
        else if (client.active)
        {
            client.CloseClient();
        }
        SceneManager.LoadScene("MainMenu");
    }
}
