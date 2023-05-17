using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class HostMenuController : MonoBehaviour
{
    public GameObject[] playersObjects = new GameObject[4];

    private Server server;

    private readonly string playersGameObjectName = "Players";
    void Start()
    {
        server = Server.Instance;
        server.ManualStart();

        GameObject playersGameObject = transform.Find(playersGameObjectName).gameObject;
        for (int i = 0; i < playersObjects.Length; i++)
        {
            playersObjects[i] = playersGameObject.transform.GetChild(i).gameObject;
            if (i == 0) { playersObjects[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = Profile.username; }
        }
    }

    public void ConnectNewClient(string username)
    {
        for (int i = 0; i < playersObjects.Length; i++)
        {
            if (playersObjects[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text == "Waiting for player...")
            {
                playersObjects[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = username;
                return;
            }
        }
        Debug.LogError("No free space to connect client");
    }

    public void DisconnectClient(string username)
    {
        for (int i = 0; i < playersObjects.Length; i++)
        {
            if (playersObjects[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text == username)
            {
                playersObjects[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Waiting for player...";
                return;
            }
        }
        Debug.LogError("Client does not exist");
    }

    public string[] GetPlayersUsernames()
    {
        string[] toReturn = new string[4];
        for (int i = 0; i < playersObjects.Length; i++)
        {
            toReturn[i] = playersObjects[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text;
        }
        return toReturn;
    }

    public void CloseServer()
    {
        server.CloseServer();
        gameObject.SetActive(false);
    }

    public void StartGame()
    {
        server.StartGame();
    }
    public void HouseRulesToggles()
    {
        GameObject toggle = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        Color newColor = toggle.GetComponent<Toggle>().isOn ? Color.green : Color.red;
        toggle.GetComponent<Image>().color = newColor;

        switch (toggle.name)
        {
            case "Stacking":
            {
                server.stacking = !server.stacking;
                break;
            }
            case "7-0":
            {
                server.sevenZero = !server.sevenZero;
                break;
            }
            case "Jump-In":
            {
                server.jumpIn = !server.jumpIn; 
                break;
            }
            case "Force Play":
            {
                server.forcePlay = !server.forcePlay;
                break;
            }
            case "No Bluffing":
            {
                server.noBluffing = !server.noBluffing;
                break;
            }
            case "Draw-To-Match":
            {
                server.drawToMatch = !server.drawToMatch;
                break;
            }
        }
    }
}
