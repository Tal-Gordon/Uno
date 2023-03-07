using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class HostMenuController : MonoBehaviour
{
    public GameObject[] users = new GameObject[4];

    private readonly string playersGameobjectName = "Players";
    void Start()
    {
        GameObject playersGameobject = transform.Find(playersGameobjectName).gameObject;
        for (int i = 0; i < users.Length; i++)
        {
            users[i] = playersGameobject.transform.GetChild(i).gameObject;
        }
    }

    void Update()
    {
        
    }

    private void ConnectNewClient(string username)
    {
        for (int i = 0; i < users.Length; i++)
        {
            if (users[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text == $"User {i + 1}")
            {
                users[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = username;
            }
        }
    }
}
