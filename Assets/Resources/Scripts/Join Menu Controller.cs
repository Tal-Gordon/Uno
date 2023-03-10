using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class JoinMenuController : MonoBehaviour
{
    public TextMeshProUGUI console;

    public List<GameObject> servers = new();

    private GameObject serversObject;
    private Client client;

    private readonly string serversPrefabPath = "Prefabs/Server";
    private readonly string serversObjectName = "Servers";
    void Start()
    {
        serversObject = transform.Find(serversObjectName).GetChild(0).GetChild(0).gameObject;
        client = GetComponent<Client>();

        UpdateServersList();
    }

    void Update()
    {
        console.text = "empty";
        foreach (var (_, _, serverName, _, _) in client.serversInfo) { console.text = serverName; }
    }

    private (int, bool) GetServerInformationByName(string serverName)
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
        return (-1, false); // Error return
    }

    private (string, int, bool) GetServerInformationByGameObject(GameObject server)
    {
        for (int i = 0; i < servers.Count; i++)
        {
            if (servers[i].Equals(server))
            {
                string serverName = servers[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text;
                int numOfConnected = int.Parse(servers[i].transform.GetChild(1).GetComponent<TextMeshProUGUI>().text.Split("/")[0]);
                bool passwordRequirement = false;
                // TODO check sprite for password requirement bool
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

    public void RerenderServers()
    {
        for (int i = 1; i < serversObject.transform.childCount; i++) 
        {
            Destroy(serversObject.transform.GetChild(i).gameObject); // We clean the playground so we can play again
        }

        List<(string, string, bool)> newServers = client.GetServersInfo();
        for (int i = 0; i < newServers.Count; i++)
        {
            RenderServer(newServers[i].Item1, newServers[i].Item2, newServers[i].Item3);
        }
        UpdateServersList();
    }
}
