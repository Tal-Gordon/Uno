using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using Unity.VisualScripting;
using System.Text;

public class JoinMenuController : MonoBehaviour
{
    public TextMeshProUGUI console;

    public List<GameObject> servers = new();
    public string[] users = new string[4];

    private GameObject serversObject;
    private GameObject joinedServer;
    private Client client;
    private Sprite _lock;

    private readonly string serversPrefabPath = "Prefabs/Server";
    private readonly string serversObjectName = "Servers";
    private readonly string joinedServerName = "Joined server";
    private readonly string lockImagePath = "Graphics/UI/lock";

    void Start()
    {
        serversObject = transform.Find(serversObjectName).GetChild(0).GetChild(0).gameObject; // Points to the location of the rendered servers gameobjects
        joinedServer = transform.Find(joinedServerName).gameObject;
        _lock = Resources.Load<Sprite>(lockImagePath);

        client = Client.Instance;
        client.ManualStart();

        UpdateServersList();
    }

    void Update()
    {
        console.text = "empty";
        foreach (var (_, _, serverName, _, _, _) in client.serversInfo) { console.text = serverName; }
    }

    private (string, int, bool) GetServerInformationByGameObject(GameObject server)
    {
        for (int i = 0; i < servers.Count; i++)
        {
            if (servers[i].Equals(server))
            {
                string serverName = servers[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text;
                int numOfConnected = int.Parse(servers[i].transform.GetChild(1).GetComponent<TextMeshProUGUI>().text.Split("/")[0]);
                bool passwordRequirement = servers[i].transform.GetChild(2).GetComponent<Image>().sprite == _lock;
;
                return (serverName, numOfConnected, passwordRequirement);
            }
        }
        return ("null", -1, false); // Error return
    }

    private void UpdateServersList() // List is dependent on the rendered servers
    {
        servers.Clear();
        if (serversObject.transform.childCount > 1)
        {
            for (int i = 1; i < serversObject.transform.childCount; i++) // We skip the first object, which is the title. God knows why I made it a child of the ScrollRect.
            {
                servers.Add(serversObject.transform.GetChild(i).gameObject);
            }
        }
    }

    public List<string> GetServersNameList()
    {
        List<string> toReturn = new();
        for (int i = 0; i < servers.Count; i++)
        {
            (string serverName, int _, bool _) = GetServerInformationByGameObject(servers[i]);
            toReturn.Add(serverName);
        }
        return toReturn;
    }

    public void RenderServer(string serverName, string amountOfConnected, bool passwordRequirement = false)
    {
        GameObject server = Resources.Load<GameObject>(serversPrefabPath);
        server.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = serverName;
        server.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text.Split("/")[0] = amountOfConnected;
        if (passwordRequirement) { server.transform.GetChild(2).GetComponent<Image>().sprite = _lock; }

        GameObject instantiatedServer = Instantiate(server, serversObject.transform);

        if (passwordRequirement) { instantiatedServer.GetComponent<Button>().onClick.AddListener(() => OpenPasswordMenu(serverName)); }
        else { instantiatedServer.GetComponent<Button>().onClick.AddListener(() => client.AskToJoin(serverName, string.Empty)); }
    }

    public void OpenPasswordMenu(string serverName)
    {
        GameObject passwordMenu = transform.Find("Password input").gameObject;
        passwordMenu.SetActive(true);
        TMP_InputField inputField = passwordMenu.transform.GetChild(0).GetComponent<TMP_InputField>();
        inputField.Select();
        passwordMenu.transform.Find("Buttons").GetChild(1).GetComponent<Button>().onClick.AddListener(() => PasswordMenuOK(serverName, inputField.text));
    }

    public void PasswordMenuOK(string serverName, string password) // workaround
    {
        client.AskToJoin(serverName, password);
        ClosePasswordMenu();
    }

    public void ClosePasswordMenu()
    {
        GameObject passwordMenu = transform.Find("Password input").gameObject;

        TMP_InputField inputField = passwordMenu.transform.GetChild(0).GetComponent<TMP_InputField>();
        inputField.Select();
        inputField.text = string.Empty;

        passwordMenu.SetActive(false);
    }

    public void RerenderServers()
    {
        try
        {
            for (int i = 1; i < serversObject.transform.childCount; i++)
            {
                Destroy(serversObject.transform.GetChild(i).gameObject);
            }

            List<(string, string, bool)> newServers = client.GetServersRenderInfo();
            for (int i = 0; i < newServers.Count; i++)
            {
                RenderServer(newServers[i].Item1, newServers[i].Item2, newServers[i].Item3);
            }
            UpdateServersList();
        }
        catch
        {
            return;
        }
    }

    public void RenderJoinedServer(string hostUsername, string player1Username, string player2Username, string player3Username)
    {
        if (!joinedServer.activeSelf) { joinedServer.SetActive(true); }
        GameObject playersObject = joinedServer.transform.Find("Players").gameObject;
        //string notConnected = "Waiting for player...";

        String[] playerOrder = new string[4];
        playerOrder[0] = hostUsername;
        playerOrder[1] = player1Username;
        playerOrder[2] = player2Username;
        playerOrder[3] = player3Username;

        for (int i = 0; i < users[i].Length; i++)
        {
            users[i] = playerOrder[i];
        }

        for (int i = 0; i < playersObject.transform.childCount; i++)
        {
            playersObject.transform.GetChild(i).GetChild(0).GetComponent<TextMeshProUGUI>().text = playerOrder[i];
        }
    }

    public void DisconnectFromServer()
    {
        joinedServer.SetActive(false);
        client.DisconnectFromServer();
    }

    public void LeaveClientele()
    {
        gameObject.SetActive(false);
        client.CloseClient();
    }
}
