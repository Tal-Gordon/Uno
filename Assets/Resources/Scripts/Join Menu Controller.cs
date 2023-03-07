using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class JoinMenuController : MonoBehaviour
{
    public List<GameObject> servers = new();

    public GameObject serversObject;
    private Client client;

    private readonly string serversPrefabPath = "Prefabs/Server";
    private readonly string serversObjectName = "Servers";
    void Start()
    {
        serversObject = transform.Find(serversObjectName).GetChild(0).GetChild(0).gameObject;
        try { transform.Find("Console").GetComponent<TextMeshProUGUI>().text = GetServerInformation("Server Name").ToString(); } catch { } // For testing purposes
        client = GetComponent<Client>();

        

        UpdateServersList();
    }

    void Update()
    {
        
    }

    private (int, bool) GetServerInformation(string serverName)
    {
        for (int i = 0; i < servers.Count; i++)
        {
            if (servers[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text == serverName)
            {
                GameObject returnServer = servers[i].transform.GetChild(1).gameObject;
                return (int.Parse(returnServer.GetComponent<TextMeshProUGUI>().text.Split("/")[0]), false); //Player count, password requirement
                // TODO check sprite for password requirement bool
            }
        }
        return (-1, false); // Error return, since player count can't be -1
    }

    private void UpdateServersList()
    {
        servers.Clear();
        if (serversObject.transform.childCount > 1)
        {
            for (int i = 1; i < serversObject.transform.childCount; i++) // We skip the first object, which is the title
            {
                servers.Add(serversObject.transform.GetChild(i).gameObject);
            }
        }
    }

    public void AddServer(string serverName, string amountOfConnected, bool passwordRequirement = false)
    {
        GameObject server = Resources.Load<GameObject>(serversPrefabPath);
        server.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = serverName;
        server.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text.Split("/")[0] = amountOfConnected;
        //server.transform.GetChild(2)

        GameObject instantiatedServer = Instantiate(server, serversObject.transform);
        instantiatedServer.GetComponent<Button>().onClick.AddListener(AskToJoinServer);

    }

    void AskToJoinServer()
    {
        GameObject server = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        string serverName = server.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text;
        client.AskToJoin(serverName);
    }
}
