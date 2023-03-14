using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class HostMenuController : MonoBehaviour
{
    public GameObject[] playersObjects = new GameObject[4];

    private Server server;
    private GameObject clientsObject;

    private readonly string playersGameObjectName = "Players";
    void Start()
    {
        GameObject playersGameObject = transform.Find(playersGameObjectName).gameObject;
        for (int i = 0; i < playersObjects.Length; i++)
        {
            playersObjects[i] = playersGameObject.transform.GetChild(i).gameObject;
        }

        server = GetComponent<Server>();
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
}
