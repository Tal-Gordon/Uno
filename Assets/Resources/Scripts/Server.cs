using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using UnityEditor.VersionControl;
using static UnityEditor.Experimental.GraphView.GraphView;
using System.Numerics;
using JetBrains.Annotations;
using Unity.VisualScripting;

public class Server : MonoBehaviour
{
    public static Server Instance;
    public HostMenuController hostMenuController;

    public string serverName;
    public bool passwordLocked;
    public string serverPassword;
    public bool active = false; // Used by Game Controller to distinguish between host and clients
    // Special rules settings
    public bool stacking = true;
    public bool sevenZero = true;
    public bool jumpIn = true;
    public bool forcePlay = true;
    public bool noBluffing = false;
    public bool drawToMatch = true;

    private Socket multicastServer;
    private Socket server;
    private GameController gameController;

    private List<(Socket, string)> connectedClients = new(); // socket, username
    private List<(Socket, string)> receivedMessages = new(); // Socket that sent message, message
    private string[] connectedUsernames = new string[3];
    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;
    private readonly ushort defaultPort = 11111;
    private ushort userPort;
    private string localIP;
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public void ManualStart()
    {
        // Trick to find local IP address
        // Connecting a UDP socket and reading it's local endpoint
        using (Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();
        }

        multicastServer = SetupMulticastSocket();
        server = SetupServer();
        ServerAcceptConnections();

        StartCoroutine(SendMessageToMulticastGroup());
        StartCoroutine(ProcessReceivedData());

        active = true;
    }
    private Socket SetupServer()
    {
        IPAddress ipAddress = IPAddress.Parse(localIP);
        IPEndPoint localEndPoint = new(ipAddress, defaultPort);

        Socket server = new(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        server.Bind(localEndPoint);
        server.Listen(3);

        return server;
    }

    public void ServerAcceptConnections()
    {
        server.BeginAccept(
            (ar) =>
            {
                Socket listener = (Socket)ar.AsyncState;
                Socket client = listener.EndAccept(ar);

                Thread clientThread = new(() => { SocketReceiveThread(client); });
                clientThread.Start();
                ServerAcceptConnections();
            }
        , server);
    }

    private Socket SetupMulticastSocket()
    {
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPAddress ipAddress = IPAddress.Parse(multicastAddress);

        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(ipAddress));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);

        IPEndPoint localEndPoint = new(ipAddress, multicastPort);
        socket.Connect(localEndPoint);

        return socket;
    }

    public IEnumerator SendMessageToMulticastGroup()
    {
        byte[] buffer = new byte[8192];
        string msg = $"multicast;{localIP};{defaultPort};{serverName};{connectedClients.Count + 1};{passwordLocked}";
        // Command identifier, server's own IP address, server name, number of already connected clients, password requirement
        // Delimiter is ;
        buffer = Encoding.Unicode.GetBytes(msg);

        while (true)
        {
            multicastServer.BeginSend(new List<ArraySegment<byte>> { new ArraySegment<byte>(buffer) }, SocketFlags.None,
                (ar) =>
                {
                    int bytesSent = multicastServer.EndSend(ar);
                }
                , null);
            yield return new WaitForSeconds(1f);
        }
    }

    // Thread to continuously receive data from any specific socket and write it to receivedMessages
    private void SocketReceiveThread(Socket socket)
    {
        byte[] buffer = new byte[8192];
        while (true)
        {
            try
            {
                int bytesRead = socket.Receive(buffer);
                string receivedMessage = Encoding.Unicode.GetString(buffer, 0, bytesRead).Trim();

                if (receivedMessage != string.Empty) 
                { 
                    lock (receivedMessages)
                    {
                        receivedMessages.Add((socket, receivedMessage));
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Thread.ResetAbort();
                Array.Clear(buffer, 0, buffer.Length);
                break;
            }
            catch (SocketException)
            {
                if (socket.Connected)
                {
                    UnityMainThreadDispatcher.Dispatcher.Enqueue(() => DisconnectClient(socket));
                }
                break;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

    private void SendMessageToClient(Socket client ,string message)
    {
        byte[] sendBuffer = new byte[8192];
        sendBuffer = Encoding.Unicode.GetBytes(message);

        client.BeginSend(new List<ArraySegment<byte>> { new ArraySegment<byte>(sendBuffer) }, SocketFlags.None,
            (ar) =>
            {
                int bytesSent = client.EndSend(ar);
                Array.Clear(sendBuffer, 0, sendBuffer.Length);
            }
            , null);
    }

    private IEnumerator ProcessReceivedData()
    {
        while (true)
        {
            lock (receivedMessages)
            {
                foreach ((Socket sendingSocket ,string receivedMessage) in receivedMessages.ToList())
                {
                    try
                    {

                        string messageToSend = GetResponse(sendingSocket, receivedMessage); // Message process

                        if (messageToSend != string.Empty && sendingSocket.Connected)
                        {
                            SendMessageToClient(sendingSocket ,messageToSend);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e); // Client likely disconnected
                    }
                }
                receivedMessages.Clear();
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private string GetResponse(Socket socket, string data)
    {
        string[] parts = data.Split(';');
        string command = parts[0];

        switch (command)
        {
            case "connect":
            {
                string clientName = parts[1];
                if (parts.Length > 3) { string password = parts[2]; }
                // TODO: verify password

                if (connectedClients.Count < 3)
                {
                    connectedClients.Add((socket, clientName));
                    hostMenuController.ConnectNewClient(clientName);
                    connectedUsernames = hostMenuController.GetPlayersUsernames();
                    return $"ok;{connectedUsernames[0]};{connectedUsernames[1]};{connectedUsernames[2]};{connectedUsernames[3]}";
                }
                else { return "full"; }
            }
            case "disconnect":
            {
                DisconnectClient(socket);
                return string.Empty;
            }
            case "playermove":
            {
                string playerIndex = parts[1];
                Card playedCard = null;

                if (parts[2] != "null")
                {
                    int cardNum = int.Parse(parts[2]);
                    if (!Enum.TryParse(parts[3], true, out CardColor cardColor))
                    {
                        Debug.LogError($"Server sent unknown color: {parts[3]}");
                    }
                    playedCard.SetNumber(cardNum);
                    playedCard.SetColor(cardColor);
                }
                gameController.PlayerFinishedTurn(playerIndex, playedCard);
                InformClients(data);
                return string.Empty;
            }
            case "getdeck":
            {
                string playerIndex = parts[1];
                string message;

                if (playerIndex == gameController.selfPlayer.GetIndex())
                {
                    message = $"senddeck;{playerIndex}";
                    List<Card> deck = new(gameController.selfPlayer.GetDeck());
                    for (int i = 0; i < deck.Count; i++)
                    {
                        string num = deck[i].GetNumber().ToString(); // 3
                        string color = deck[i].GetColor().ToString(); // yellow
                        if (num.Length == 1) { num = "0" + num; } // 03
                        string cardInfo = num + color; // 03yellow

                        message += $";{cardInfo}";
                    }
                    return message;
                }
                else
                {

                    SendMessageToClient(GetSocketByPlayerIndex(playerIndex), $"getdeck;{playerIndex}");
                    return string.Empty;
                }
            }
            case "senddeck":
            {
                string playerIndex = parts[1];
                string message = $"senddeck;{playerIndex}";
                for (int i = 1; i < parts.Length; i++)
                {
                    message += $";{parts[i]}";
                }
                SendMessageToClient(GetSocketByPlayerIndex(playerIndex), message);
                return string.Empty;
            }
            default:
            {
                // Handle unknown commands
                Debug.LogError($"Client {socket} sent unknown command: {command}");
                return string.Empty;
            }
        }
    }

    private void InformClients(string message)
    {
        for (int i = 0; i < connectedClients.Count; i++)
        {
            SendMessageToClient(connectedClients[i].Item1, message);
        }
    }

    public void DisconnectClient(Socket client)
    {
        for (int i = 0; i < connectedClients.Count; i++)
        {
            if (connectedClients[i].Item1 == client)
            {
                hostMenuController.DisconnectClient(connectedClients[i].Item2);
                connectedClients.Remove(connectedClients[i]);

                connectedUsernames = hostMenuController.GetPlayersUsernames();
                InformClients($"userleft;{connectedUsernames[0]};{connectedUsernames[1]};{connectedUsernames[2]};{connectedUsernames[3]}");
                break;
            }
        }
    }

    public void CloseServer()
    {
        StopAllCoroutines();
        for (int i = 0; i < connectedClients.Count; i++)
        {
            SendMessageToClient(connectedClients[i].Item1, "disconnect");
        }
        multicastServer.Close();
        server.Close();
        active = false;
        if (SceneManager.GetActiveScene().name == "Game")
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
// --------------------------------------------------------------------------------------------------------------------------------------------------
    public void StartGame()
    {
        StopCoroutine(SendMessageToMulticastGroup());

        for (int i = 0; i < connectedClients.Count; i++)
        {
            SendMessageToClient(connectedClients[i].Item1, $"gamestart;{i + 2}"); // We account for the zero-based index and begin counting at 2, because host is always 1
        }
        SceneManager.LoadScene("Game");
        SceneManager.activeSceneChanged += ChangedScene;
    }
    public void ChangedScene(Scene current, Scene next)
    {
        GameObject[] roots = next.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].TryGetComponent(out gameController)) 
            {
                if (connectedClients.Count != 3)
                {
                    for (int j = 4; (4 - j) < (3 - connectedClients.Count); j--)
                    {
                        GameObject playerObject = gameController.GetPlayerObjectByIndex(j.ToString());
                        Destroy(playerObject.GetComponent<FakePlayer>());
                        playerObject.GetOrAddComponent<Player>().aiDriven = true;
                        playerObject.GetComponent<Player>().playerName = "AI Player";
                    }
                }
                StartNewGame();
                break; 
            }
        }
    }
    public void PlayerMadeMove(string playerIndex, Card playedCard)
    {
        string message = $"playermove;{playerIndex};";

        if (playedCard != null)
        {
            message += $"{playedCard.GetNumber()};{playedCard.GetColor()}";
        }
        else
        {
            message += "null";
        }

        InformClients(message);
    }
    public void StartNewGame()
    {
        StartCoroutine(gameController.LateStart(0.5f));
    }
    public void CalledOutUnunoed(string callingPlayerIndex)
    {
        InformClients($"calledout;{callingPlayerIndex}");
        //for (int i = 0; i < players.Count; i++)
        //{
        //    if (players[i].GetDeck().Count == 1 && !players[i].GetUnoed())
        //    {
        //        ForceDrawCards(players[i], 2);
        //        players[i].UnshowUnoButton();
        //    }
        //    players[i].UnshowCallOutButton();
        //}
    }
    public void SwapHands(string chosenPlayerIndex)
    {
        SendMessageToClient(GetSocketByPlayerIndex(chosenPlayerIndex), $"getdeck");
    }
    private Socket GetSocketByPlayerIndex(string index)
    {
        return connectedClients[int.Parse(index) - 2].Item1;
    }
}