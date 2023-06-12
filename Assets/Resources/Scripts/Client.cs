using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Net.Sockets;
using System.Net;
using System;
using System.Text;
using System.Threading;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;

public class Client : MonoBehaviour
{
    public static Client Instance;
    public JoinMenuController joinMenuController;

    public List<(string IpAddress, ushort port, string serverName, string numOfConnected, bool passReq, bool isAlive)> serversInfo = new();
    public bool active = false;

    private Socket multicastClient;
    private Socket client;
    private Thread multicastReceiveThread;
    private Thread serverReceiveThread;
    private GameController gameController;

    private List<string> receivedMessages = new();
    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;
    private string index = string.Empty;
    // Special rules settings
    public bool stacking = false;
    public bool sevenZero = false;
    public bool jumpIn = false;
    public bool forcePlay = false;
    public bool noBluffing = false;
    public bool drawToMatch = false;
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
        multicastClient = SetupMulticastClient();

        multicastReceiveThread = new Thread(() => SocketReceiveThread(multicastClient));
        multicastReceiveThread.Start();

        StartCoroutine(ProcessReceivedMulticastData());
        StartCoroutine(ProcessReceivedServerData());
        StartCoroutine(CheckAliveServers());

        active = true;
    }
    private Socket SetupMulticastClient()
    {
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        IPEndPoint localEndPoint = new(IPAddress.Any, multicastPort);
        socket.Bind(localEndPoint);

        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(multicastAddress), IPAddress.Any));

        return socket;
    }
    private Socket SetupClient(string ipAddress, ushort port)
    {
        IPAddress ipAddressToConnect = IPAddress.Parse(ipAddress);
        IPEndPoint remoteEndPoint = new(ipAddressToConnect, port);

        Socket client = new(ipAddressToConnect.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            var connectResult = client.BeginConnect(remoteEndPoint, null, null);

            // Wait for the connection to complete asynchronously
            bool isConnected = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5), true);

            if (isConnected)
            {
                client.EndConnect(connectResult);
            }
            else
            {
                Debug.LogError("Connection timed out");
                client.Close();
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            client.Close();
            return null;
        }

        return client;
    }

    // Thread to continuously receive data from a specific socket, be it multicast or direct
    // Every received message is written to a list, so Unity's main thread can access and process it at it's convenience
    private void SocketReceiveThread(Socket socket)
    {
        byte[] buffer = new byte[8192];
        while (true)
        {
            try
            {
                int bytesRead = socket.Receive(buffer);
                string receivedMessage = Encoding.Unicode.GetString(buffer, 0, bytesRead).Trim();

                lock (receivedMessages)
                {
                    receivedMessages.Add(receivedMessage);
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
                    UnityMainThreadDispatcher.Dispatcher.Enqueue(() => DisconnectFromServer());
                }
                break;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                break;
            }
        }
    }
    /// <summary>
    /// Sends a message to the server
    /// </summary>
    /// <param name="message"></param>
    private void SendMessageToServer(string message)
    {
        // Method assumes client is already connected to server
        byte[] buffer = new byte[8192];
        buffer = Encoding.Unicode.GetBytes(message);

        client.BeginSend(new List<ArraySegment<byte>> { new ArraySegment<byte>(buffer) }, SocketFlags.None,
            (ar) =>
            {
                int bytesSent = client.EndSend(ar);
                Array.Clear(buffer, 0, buffer.Length);
            }
            , null);
    }

    private void ProcessSocketReceivedData(Socket socket)
    {
        lock (receivedMessages)
        {
            foreach (string receivedMessage in receivedMessages.ToList())
            {
                try
                {
                    Debug.Log($"client received: '{receivedMessage}'");
                    if (receivedMessage == string.Empty)
                    {
                        socket.Close();
                        continue;
                    }
                    string messageToSend = GetResponse(receivedMessage); // Message process

                    if (messageToSend != string.Empty)
                    {
                        SendMessageToServer(messageToSend);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e); // Likely connection closed by server
                    // Check if socket is connected, close if not
                    bool part1 = socket.Poll(1000, SelectMode.SelectRead);
                    bool part2 = socket.Available == 0;
                    if ((part1 && part2) || !socket.Connected)
                    {
                        socket.Close();
                    }
                }
            }
            receivedMessages.Clear();
        }
    }

    private IEnumerator ProcessReceivedMulticastData()
    {
        while (SceneManager.GetActiveScene().name == "MainMenu")
        {
            try
            {
                ProcessSocketReceivedData(multicastClient);
            }
            catch
            {
                continue;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator ProcessReceivedServerData()
    {
        while (true)
        {
            if (client != null)
            {
                if (client.Connected) { ProcessSocketReceivedData(client); }
            }
            yield return new WaitForSeconds(2f);
        }
    }

    public void AskToJoin(string serverName, string password)
    {
        string ipAddress = string.Empty;
        ushort port = ushort.MinValue;

        for (int i = 0; i < serversInfo.Count; i++)
        {
            if (serversInfo[i].serverName == serverName)
            {
                ipAddress = serversInfo[i].IpAddress;
                port = serversInfo[i].port;
            }
        }

        client = SetupClient(ipAddress, port);

        string messageToSend = $"connect;{Profile.username}";
        // command identifier "connect", username
        if (password != string.Empty) { messageToSend += $";{password}"; }

        if (client != null)
        {
            SendMessageToServer(messageToSend);

            // Start the thread to receive data
            serverReceiveThread = new Thread(() => SocketReceiveThread(client));
            serverReceiveThread.Start();
        }
    }

    public void DisconnectFromServer()
    {
        if (client.Connected) { SendMessageToServer($"disconnect"); }
        serverReceiveThread.Abort();
        serverReceiveThread.Join();
        client.Close();
        if (SceneManager.GetActiveScene().name == "Game")
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    private string GetResponse(string data)
    {
        // Responsible for figuring out how to respond to server. if returns string.Empty, it means client won't respond 
        string[] parts = data.Split(';');
        string command = parts[0];

        switch (command)
        {
            case "ok":
            {
                string hostUsername = parts[1];
                string player1Username = parts[2];
                string player2Username = parts[3];
                string player3Username = parts[4];

                RenderJoinedServer(hostUsername, player1Username, player2Username, player3Username);

                return string.Empty;
            }
            case "rejected":
            {
                Debug.LogError("Login rejected: wrong password");

                return string.Empty;
            }
            case "full":
            {
                Debug.LogError("Login rejected: server is full");

                return string.Empty;
            }
            case "multicast":
            {
                string serverIpAddress = parts[1];
                ushort serverPort = ushort.Parse(parts[2]);
                string serverName = parts[3];
                string numOfConnected = parts[4];
                bool passReq = bool.Parse(parts[5]);

                bool serverExists = false;
                lock (serversInfo)
                {
                    for (int i = 0; i < serversInfo.Count; i++)
                    {
                        if (serversInfo[i].IpAddress == serverIpAddress) // We find the server that sent the multicast in our list
                        {
                            serverExists = true;

                            string tempVar_numOfConnected = serversInfo[i].numOfConnected;
                            // Set isAlive property as true, and update numOfConnected value
                            serversInfo[i] = (serversInfo[i].IpAddress, serversInfo[i].port, serversInfo[i].serverName, numOfConnected, serversInfo[i].passReq, true);
                            if (tempVar_numOfConnected != numOfConnected) { UpdateRenderedServers(); }
                        }
                    }

                    if (!serverExists) // If the server doesn't exist in the list, it means it's new
                    {
                        serversInfo.Add((serverIpAddress, serverPort, serverName, numOfConnected, passReq, true));
                        UpdateRenderedServers();
                    }
                }

                return string.Empty;
            }
            case "disconnect":
            {
                Debug.LogError("Server closed connection");
                DisconnectFromServer();

                return string.Empty;
            }
            case "userleft":
            {
                //string leftUsername = parts[1];
                //joinMenuController.RemoveUser(leftUsername);

                //return string.Empty;
                string hostUsername = parts[1];
                string player1Username = parts[2];
                string player2Username = parts[3];
                string player3Username = parts[4];

                if (SceneManager.GetActiveScene().name == "Main Menu")
                {
                    RenderJoinedServer(hostUsername, player1Username, player2Username, player3Username); 
                }

                return string.Empty;
            }
            case "gamestart":
            {
                index = parts[1];
                stacking =    parts[2] == "t";
                sevenZero =   parts[3] == "t";
                jumpIn =      parts[4] == "t";
                forcePlay =   parts[5] == "t";
                noBluffing =  parts[6] == "t";
                drawToMatch = parts[7] == "t";
                PrepareForGame();
                return string.Empty;
            }
            case "playermove":
            {
                string playerIndex = parts[1];
                int cardNum = 0;
                CardColor cardColor = CardColor.Wild;

                if (parts[2] != "null")
                {
                    if (!int.TryParse(parts[2].Substring(0, 2), out cardNum))
                    {
                        Debug.LogError($"Invalid number: {parts[2].Substring(0, 2)}");
                        return string.Empty;
                    }

                    if (!Enum.TryParse(parts[2].Substring(2), out cardColor))
                    {
                        Debug.LogError($"Invalid color: {parts[2].Substring(2)}");
                        return string.Empty;
                    }
                    try
                    {
                        gameController.PlayerFinishedTurn(playerIndex, cardNum, cardColor, false);
                    }
                    catch
                    {
                        StartCoroutine(TryRegisterPlayerMove(playerIndex, cardNum, cardColor, false));
                    }
                    return string.Empty;
                }
                try
                {
                    StartCoroutine(TryRegisterPlayerMove(playerIndex, cardNum, cardColor, true));
                }
                catch (Exception)
                {

                    throw;
                }
                return string.Empty;
            }
            case "calledout":
            {
                string callingPlayerIndex = parts[1];
                if (gameController.selfPlayer.GetDeck().Count == 1 && !gameController.selfPlayer.GetUnoed()) 
                {
                    for (int i = 0; i < 2; i++)
                    {
                        DrawCardFromServer();
                    }
                }
                gameController.selfPlayer.UnshowCallOutButton();
                return string.Empty;
            }
            case "getdeck":
            {
                string callingPlayerIndex = parts[1];
                string message = $"senddeck;{callingPlayerIndex}";
                List<Card> deck = new(gameController.selfPlayer.GetDeck());
                message += GetStringRepresentationFromDeck(deck);
                return message;
            }
            //case "senddeck":
            //{
            //    string playerIndex = parts[1];
            //    List<Card> deck = new();
            //    for (int i = 2; i < parts.Length; i++) // Skipping the command and the index; expected input example: 03yellow
            //    {
            //        Card card = null;
            //        int num;

            //        num = int.Parse(parts[i].Substring(0, 2));
            //        Enum.TryParse(parts[i].Substring(2), out CardColor color);

            //        card.SetNumber(num);
            //        card.SetColor(color);

            //        deck.Add(card);
            //    }
            //    gameController.SwapHands(gameController.selfPlayer.GetIndex(), deck);
            //    return string.Empty;
            //}
            case "setdeck":
            {
                string playerToSet = parts[1];
                List<Card> deck = new();
                for (int i = 2; i < parts.Length; i++) // Skipping the command and the index; expected input example: 03yellow
                {
                    Card card = null;
                    int num;

                    num = int.Parse(parts[i].Substring(0, 2));
                    Enum.TryParse(parts[i].Substring(2), out CardColor color);

                    card.SetNumber(num);
                    card.SetColor(color);

                    deck.Add(card);
                }
                gameController.GetIPlayerByIndex(playerToSet).SetDeck(deck);
                return string.Empty;
            }
            case "drawedcard":
            {
                string drawingPlayerIndex = parts[1];
                if (drawingPlayerIndex != gameController.selfPlayer.GetIndex())
                {
                    gameController.GetIPlayerByIndex(drawingPlayerIndex).DrawCard();
                }

                return string.Empty;
            }
            case "drawcard":
            {
                string cardInfo = parts[1];

                if (!int.TryParse(cardInfo.Substring(0, 2), out int num))
                {
                    Debug.LogError($"Invalid number: {cardInfo.Substring(0, 2)}");
                    return string.Empty;
                }

                if (!Enum.TryParse(cardInfo.Substring(2), out CardColor color))
                {
                    Debug.LogError($"Invalid color {cardInfo.Substring(2)}");
                    return string.Empty; 
                }
                gameController.selfPlayer.DrawCard(num, color);

                return string.Empty;
            }
            case "drawhand":
            {
                for (int i = 1; i < 8; i++)
                {
                    if (!int.TryParse(parts[i].Substring(0, 2), out int num))
                    {
                        Debug.LogError($"Invalid number: {parts[i].Substring(0, 2)}");
                        return string.Empty;
                    }

                    if (!Enum.TryParse(parts[i].Substring(2), out CardColor color))
                    {
                        Debug.LogError($"Invalid color {parts[i].Substring(2)}");
                        return string.Empty;
                    }
                    gameController.selfPlayer.DrawCard(num, color);
                }
                return string.Empty;
            }
            default:
            {
                // Handle unknown commands
                Debug.LogError($"Server sent unknown command: {command}");
                if (command != "multicast")
                {
                    DisconnectFromServer();
                }
                return string.Empty;
            }
        }
    }

    private IEnumerator CheckAliveServers()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f);

            lock (serversInfo)
            {
                for (int i = 0; i < serversInfo.Count; i++)
                {
                    if (!serversInfo[i].isAlive)
                    {
                        serversInfo.Remove(serversInfo[i]);
                    }
                }
                UpdateRenderedServers();
                for (int i = 0; i < serversInfo.Count; i++)
                {
                    serversInfo[i] = (serversInfo[i].IpAddress, serversInfo[i].port, serversInfo[i].serverName, serversInfo[i].numOfConnected, serversInfo[i].passReq, false);
                }
            }
        }
    }

    public List<(string, string, bool)> GetServersRenderInfo()
    {
        // Returns info about servers required for render: server name, amount of connected players, password requirement
        List<(string, string, bool)> toReturn = new();
        for (int i = 0; i < serversInfo.Count; i++)
        {
            toReturn.Add((serversInfo[i].serverName, serversInfo[i].numOfConnected, serversInfo[i].passReq));
        }
        return toReturn;
    }

    public void RenderJoinedServer(string hostUsername, string player1Username, string player2Username, string player3Username)
    {
        joinMenuController.RenderJoinedServer(hostUsername, player1Username, player2Username, player3Username);
    }
    public void CloseClient()
    {
        if (client != null)
        {
            StopAllCoroutines();
            client.Close();
            active = false;
        }
    }
    private void UpdateRenderedServers()
    {
        joinMenuController.RerenderServers();
    }
// -------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private void PrepareForGame()
    {
        StopCoroutine(CheckAliveServers());
        SceneManager.LoadScene("Game");
        SceneManager.activeSceneChanged += ChangedScene;
    }
    private void ChangedScene(Scene current, Scene next)
    {
        GameObject[] roots = next.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].TryGetComponent(out gameController)) 
            { 
                gameController.IndexPlayers(index);

                gameController.stacking = stacking;
                gameController.sevenZero = sevenZero;
                gameController.jumpIn = jumpIn;
                gameController.forcePlay = forcePlay;
                gameController.noBluffing = noBluffing;
                gameController.drawToMatch = drawToMatch;

                StartNewGame();
                break; 
            }
        }
    }
    public void StartNewGame()
    {
        StartCoroutine(gameController.LateStart(0.5f));
    }
    public void PlayerMadeMove(Card playedCard)
    {
        string message = $"playermove;{gameController.selfPlayer.GetIndex()};";
        if (playedCard != null)
        {
            string num = playedCard.GetNumber().ToString(); // 3
            string color = playedCard.GetColor().ToString(); // yellow
            if (num.Length == 1) { num = "0" + num; } // 03
            string cardInfo = num + color; // 03yellow

            message += $"{cardInfo}";
        }
        else
        {
            message += "null";
        }

        SendMessageToServer(message);
    }
    public void PlayedSeven(string chosenPlayerIndex)
    {
        SendMessageToServer($"getdeck;{chosenPlayerIndex};{GetStringRepresentationFromDeck(gameController.selfPlayer.GetDeck())}");
    }
    /// <summary>
    /// DO NOT USE, BROKEN
    /// </summary>
    /// <param name="deckRepresentation"></param>
    /// <returns></returns>
    private List<Card> GetDeckFromStringRepresentation(string deckRepresentation)
    {
        List<Card> deck = new();
        string[] parts = deckRepresentation.Split(";");
        for (int i = 0; i < parts.Length; i++)
        {
            Card card = null;

            if (parts[i].Length < 3)
            {
                Debug.LogError($"Invalid input string at index {i}: {parts[i]}");
                continue; // skip this input string and move on to the next one
            }

            if (!int.TryParse(parts[i].Substring(0, 2), out int num))
            {
                Debug.LogError($"Invalid number at index {i}: {parts[i]}");
                continue; // skip this input string and move on to the next one
            }

            if (!Enum.TryParse(parts[i].Substring(2), out CardColor color))
            {
                Debug.LogError($"Invalid color at index {i}: {parts[i]}");
                continue; // skip this input string and move on to the next one
            }

            card.SetNumber(num);
            card.SetColor(color);

            deck.Add(card);
        }
        return deck;
    }
    private string GetStringRepresentationFromDeck(List<Card> deck)
    {
        string[] cardInfos = new string[deck.Count];
        for (int i = 0; i < deck.Count; i++)
        {
            string num = deck[i].GetNumber().ToString(); // 3
            if (num.Length == 1) { num = "0" + num; } // 03
            string color = deck[i].GetColor().ToString(); // yellow
            string cardInfo = num + color; // 03yellow

            cardInfos[i] = cardInfo;
        }
        return string.Join(";", cardInfos);
    }
    public void DrawCardFromServer()
    {
        SendMessageToServer($"drawcard;{gameController.selfPlayer.GetIndex()}");
    }
    public void DrawHand()
    {
        SendMessageToServer($"drawhand");
    }
    private IEnumerator TryRegisterPlayerMove(string playerIndex, int cardNum, CardColor cardColor, bool _null)
    {
        yield return new WaitForSeconds(1.1f);
        gameController.PlayerFinishedTurn(playerIndex, cardNum, cardColor, _null);
    }
}