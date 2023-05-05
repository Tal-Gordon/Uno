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
using Unity.VisualScripting;
using UnityEditor.VersionControl;

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
    private List<(string, string)> clientsNewHands = new(); // player index, hand in string representation
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
                string chosenPlayerIndex = parts[1];
                string sentDeck = string.Empty;
                string callingPlayerIndex = GetPlayerIndexBySocket(socket);

                for (int i = 2; i < parts.Length; i++) // Skipping command and player index
                {
                    sentDeck += $";{parts[i]}";
                }
                //if (chosenPlayerIndex == gameController.selfPlayer.GetIndex())
                //{
                //    message = $"senddeck;{chosenPlayerIndex}";
                //    List<Card> deck = new(gameController.selfPlayer.GetDeck());
                //    for (int i = 0; i < deck.Count; i++)
                //    {
                //        string num = deck[i].GetNumber().ToString(); // 3
                //        string color = deck[i].GetColor().ToString(); // yellow
                //        if (num.Length == 1) { num = "0" + num; } // 03
                //        string cardInfo = num + color; // 03yellow

                //        message += $";{cardInfo}";
                //    }
                //    return message;
                //}
                //else
                //{
                //    SendMessageToClient(GetSocketByPlayerIndex(chosenPlayerIndex), $"getdeck;{chosenPlayerIndex}");
                //    return string.Empty;
                //}
                clientsNewHands.Add((chosenPlayerIndex, sentDeck));
                if (gameController.GetPlayerObjectByIndex(chosenPlayerIndex).GetComponent<Player>())
                {
                    List<Card> deck = new(gameController.GetPlayerObjectByIndex(chosenPlayerIndex).GetComponent<Player>().GetDeck());
                    string stringDeck = GetStringRepresentationFromDeck(deck);
                    clientsNewHands.Add((callingPlayerIndex, stringDeck));
                }
                else
                {
                    SendMessageToClient(GetSocketByPlayerIndex(chosenPlayerIndex), $"getdeck;{callingPlayerIndex}");
                }

                return string.Empty;
            }
            case "senddeck":
            {
                string callingPlayerIndex = parts[1];
                string message = string.Empty;
                for (int i = 2; i < parts.Length; i++) // Skipping command and player index
                {
                    message += $";{parts[i]}";
                }
                clientsNewHands.Add((callingPlayerIndex, message));

                int topCardNum = gameController.GetTopCard().GetNumber();
                int amountOfHandsRegistered = clientsNewHands.Count;

                if ((topCardNum == 7 && amountOfHandsRegistered == 2) || (topCardNum == 0 && amountOfHandsRegistered == 4))
                {
                    SwapHands();
                }
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
                        //Destroy(playerObject.GetComponent<FakePlayer>());
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
        Debug.Log($"server was told that player {playerIndex} just played");
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
        GameObject nextPlayer = gameController.GetPlayerObjectByIndex(gameController.GetNextPlayerIndex(playerIndex));
        if (nextPlayer.GetComponent<Player>()) // Either self or ai player
        {
            Debug.Log($"server orders player {nextPlayer.GetComponent<Player>().GetIndex()} to play");
            nextPlayer.GetComponent<Player>().GetTurnAndCheckCards();
        }
        // No need to explicitly order clients to play, they should know to give themselves a turn when it's due
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
    public void GetClientDeck(string chosenPlayerIndex)
    {
        SendMessageToClient(GetSocketByPlayerIndex(chosenPlayerIndex), $"getdeck");
    }
    private Socket GetSocketByPlayerIndex(string index)
    {
        return connectedClients[int.Parse(index) - 2].Item1;
    }
    private string GetPlayerIndexBySocket(Socket socket)
    {
        for (int i = 0; i < connectedClients.Count; i++)
        {
            if (connectedClients[i].Equals(socket))
            {
                return (i + 2).ToString();
            }
        }
        return null;
    }
    private void SwapHands()
    {
        for (int i = 0; i < clientsNewHands.Count; i++)
        {
            if (gameController.GetPlayerObjectByIndex(clientsNewHands[i].Item1).GetComponent<Player>()) // Checking if the player is self/ai or connected client
            {
                Player player = gameController.GetPlayerObjectByIndex(clientsNewHands[i].Item1).GetComponent<Player>();
                List<Card> newDeck = GetDeckFromStringRepresentation(clientsNewHands[i].Item2);
                player.SetDeck(newDeck);
            }
            SendMessageToClient(connectedClients[i].Item1, $"setdeck;{clientsNewHands[i].Item1};{clientsNewHands[i].Item2}");
        }
        clientsNewHands.Clear();
    }
    private List<Card> GetDeckFromStringRepresentation(string deckRepresentation)
    {
        List<Card> deck = new();
        string[] parts = deckRepresentation.Split(";");
        for (int i = 0; i < parts.Length; i++)
        {
            Card card = null;
            int num;

            num = int.Parse(parts[i].Substring(0, 2));
            Enum.TryParse(parts[i].Substring(2), out CardColor color);

            card.SetNumber(num);
            card.SetColor(color);

            deck.Add(card); 
        }
        return deck;
    }
    private string GetStringRepresentationFromDeck(List<Card> deck)
    {
        string deckString = string.Empty;
        for (int i = 0; i < deck.Count; i++)
        {
            string num = deck[i].GetNumber().ToString(); // 3
            if (num.Length == 1) { num = "0" + num; } // 03
            string color = deck[i].GetColor().ToString(); // yellow
            string cardInfo = num + color; // 03yellow

            deckString += $";{cardInfo}";
        }
        return deckString;
    }
    /// <summary>
    /// Only if host played a seven
    /// </summary>
    /// <param name="chosenPlayerIndex"></param>
    public void PlayedSeven(string chosenPlayerIndex)
    {
        clientsNewHands.Add((chosenPlayerIndex, GetStringRepresentationFromDeck(new(gameController.selfPlayer.GetDeck()))));
        if (gameController.GetPlayerObjectByIndex(chosenPlayerIndex).GetComponent<Player>()) // ai
        {
            Player aiPlayer = gameController.GetPlayerObjectByIndex(chosenPlayerIndex).GetComponent<Player>();
            
            clientsNewHands.Add((gameController.selfPlayer.GetIndex(), GetStringRepresentationFromDeck(new(aiPlayer.GetDeck()))));
            SwapHands();
            return;
        }
        SendMessageToClient(GetSocketByPlayerIndex(chosenPlayerIndex), $"getdeck;{gameController.selfPlayer.GetIndex()}");
    }
    public void PlayedZero()
    {
        for (int i = 0; i < connectedClients.Count; i++)
        {
            
        }
    }
}