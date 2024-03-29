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
using System.Security.Cryptography;

public class Server : MonoBehaviour
{
    public static Server Instance;
    public HostMenuController hostMenuController;

    public string serverName;
    public bool passwordLocked;
    public (string, byte[]) serverPassword; // Hash, salt
    public bool active = false; // Used by Game Controller to distinguish between host and clients
    // Special rules settings
    public bool stacking = false;
    public bool sevenZero = false;
    public bool jumpIn = false;
    public bool forcePlay = false;
    public bool noBluffing = false;
    public bool drawToMatch = false;

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

        // Trick to find local IP address
        // Connecting a UDP socket and reading it's local endpoint
        using (Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();
        }
    }
    public void ManualStart()
    {
        active = true;

        multicastServer = SetupMulticastSocket();
        server = SetupServer();
        ServerAcceptConnections();

        StartCoroutine(SendMessageToMulticastGroup());
        StartCoroutine(ProcessReceivedData());
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
                if (SceneManager.GetActiveScene().name == "MainMenu")
                {
                    Socket listener = (Socket)ar.AsyncState;
                    Socket client = listener.EndAccept(ar);

                    Thread clientThread = new(() => { SocketReceiveThread(client); });
                    clientThread.Start();
                    ServerAcceptConnections(); 
                }
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

        while (active && SceneManager.GetActiveScene().name == "MainMenu")
        {
            string msg = $"multicast;{localIP};{defaultPort};{serverName};{connectedClients.Count + 1};{passwordLocked}";
            // Command identifier, server's own IP address, server's port, server name, number of already connected clients, password requirement

            buffer = Encoding.Unicode.GetBytes(msg);

            multicastServer.BeginSend(new List<ArraySegment<byte>> { new ArraySegment<byte>(buffer) }, SocketFlags.None,
                (ar) =>
                {
                    int bytesSent = multicastServer.EndSend(ar);
                    Array.Clear(buffer, 0, buffer.Length);
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
        Debug.Log(message);
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
                bool passwordVerification = !passwordLocked; // If passwordLocked, verification required. else, verified automatically.

                if (parts.Length > 3 && passwordLocked) 
                { 
                    string submittedPassword = parts[2]; 
                    passwordVerification = VerifyPassword(submittedPassword, serverPassword.Item1, serverPassword.Item2);
                }

                if (passwordVerification)
                {
                    if (connectedClients.Count < 3)
                    {
                        connectedClients.Add((socket, clientName));
                        hostMenuController.ConnectNewClient(clientName);
                        connectedUsernames = hostMenuController.GetPlayersUsernames();
                        return $"ok;{connectedUsernames[0]};{connectedUsernames[1]};{connectedUsernames[2]};{connectedUsernames[3]}";
                    }
                    else { return "full"; } 
                }
                else { return "rejected"; }
            }
            case "disconnect":
            {
                DisconnectClient(socket);
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

                    gameController.PlayerFinishedTurn(playerIndex, cardNum, cardColor, false);
                    return string.Empty;
                }
                gameController.PlayerFinishedTurn(playerIndex, cardNum, cardColor, true);
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
                    SetNewHands();
                }
                return string.Empty;
            }
            case "drawcard":
            {
                string drawingPlayerIndex = parts[1];
                StartCoroutine(InformClientsLater($"drawedcard;{drawingPlayerIndex}")); // hardfix
                StartCoroutine(TryRegisterPlayerDraw(drawingPlayerIndex));

                (int, CardColor) drawedCard = gameController.DrawCard();

                string num = drawedCard.Item1.ToString(); // 3
                if (num.Length == 1) { num = "0" + num; } // 03
                string color = drawedCard.Item2.ToString(); // yellow
                string cardInfo = num + color; // 03yellow

                return $"drawcard;{cardInfo}";
            }
            case "drawhand":
            {
                string message = "drawhand";
                for (int i = 0; i < 7; i++)
                {
                    (int, CardColor) drawedCard = gameController.DrawCard();

                    string num = drawedCard.Item1.ToString(); // 3
                    if (num.Length == 1) { num = "0" + num; } // 03
                    string color = drawedCard.Item2.ToString(); // yellow
                    string cardInfo = num + color; // 03yellow

                    message += $";{cardInfo}";
                }
                return message;
            }
            case "uno":
            {
                string callingPlayerIndex = parts[1];
                InformClients($"unoed;{callingPlayerIndex}");
                gameController.selfPlayer.UnshowCallOutButton();

                return string.Empty;
            }
            case "callout":
            {
                string callingPlayerIndex = parts[1];
                InformClients($"calledout;{callingPlayerIndex}");
                gameController.CalledOut();

                return string.Empty;
            }
            default:
            {
                // Handle unknown commands
                Debug.LogError($"Client {socket} sent unknown command: {command}");
                DisconnectClient(socket);
                return string.Empty;
            }
        }
    }

    private string HashPassword(string password, byte[] salt)
    {
        int iterations = 10000; // Number of iterations
        int hashByteSize = 32; // Size of the hash in bytes

        Rfc2898DeriveBytes pbkdf2 = new(password, salt, iterations);
        byte[] hash = pbkdf2.GetBytes(hashByteSize);

        // Combine the salt and hash into a single byte array
        byte[] hashWithSalt = new byte[salt.Length + hash.Length];
        Array.Copy(salt, 0, hashWithSalt, 0, salt.Length);
        Array.Copy(hash, 0, hashWithSalt, salt.Length, hash.Length);

        return Convert.ToBase64String(hashWithSalt);
    }

    public string HashPassword(string password, out byte[] salt)
    {
        int saltByteSize = 16; // Size of the salt in bytes

        // Generate a random salt
        using (var rng = RandomNumberGenerator.Create())
        {
            salt = new byte[saltByteSize];
            rng.GetBytes(salt);
        }

        return HashPassword(password, salt);
    }

    private bool VerifyPassword(string password, string storedPassword, byte[] salt)
    {
        string HashedGivenPassword = HashPassword(password, salt);
        return storedPassword.Equals(HashedGivenPassword);
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
        if (SceneManager.GetActiveScene().name == "Game")
        {
            GameObject playerObject = gameController.GetPlayerObjectByIndex(GetPlayerIndexBySocket(client));
            Destroy(playerObject.GetComponent<FakePlayer>());
            playerObject.GetOrAddComponent<Player>().aiDriven = true;
            playerObject.GetComponent<Player>().playerName = "AI Player";
        }
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
        if (server != null && server.Connected)
        {
            server.Close();
        }
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
        string _stacking = stacking ?       "t" : "f";
        string _sevenZero = sevenZero ?     "t" : "f";
        string _jumpIn = jumpIn ?           "t" : "f";
        string _forcePlay = forcePlay ?     "t" : "f";
        string _noBluffing = noBluffing ?   "t" : "f";
        string _drawToMatch = drawToMatch ? "t" : "f";

        for (int i = 0; i < connectedClients.Count; i++)
        {
            SendMessageToClient(connectedClients[i].Item1, $"gamestart;{i + 2};{_stacking};{_sevenZero};{_jumpIn};{_forcePlay};{_noBluffing};{_drawToMatch}"); // We account for the zero-based index and begin counting at 2, because host is always 1
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
                gameController.stacking = stacking;
                gameController.sevenZero = sevenZero;
                gameController.jumpIn = jumpIn;
                gameController.forcePlay = forcePlay;
                gameController.noBluffing = noBluffing;
                gameController.drawToMatch = drawToMatch;
                StartCoroutine(StartNewGame());
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
            string num = playedCard.GetNumber().ToString(); // 3
            if (num.Length == 1) { num = "0" + num; } // 03
            string color = playedCard.GetColor().ToString(); // yellow
            string cardInfo = num + color; // 03yellow

            message += $"{cardInfo}";
        }
        else
        {
            message += "null";
        }

        InformClients(message);

        //GameObject nextPlayer = gameController.GetPlayerObjectByIndex(gameController.GetNextPlayerIndex(playerIndex));
        //bool inputRequired = false; // Set to true if additional input is required from the player
        //if (playedCard != null)
        //{
        //    switch (playedCard.GetNumber())
        //    {
        //        case 7:
        //        {
        //            if (sevenZero)
        //            {
        //                inputRequired = true;
        //            }
        //            break;
        //        }
        //        case 14:
        //        case 15:
        //        {
        //            if (playedCard.GetColor() == CardColor.Wild)
        //            {
        //                inputRequired = true;
        //            }
        //            break;
        //        }
        //    } 
        //}
        //if (nextPlayer.GetComponent<Player>() && nextPlayer.GetComponent<Player>().GetDeck().Count > 0 && !inputRequired) // Either self or ai player
        //{
        //    Debug.Log($"server orders player {nextPlayer.GetComponent<Player>().GetIndex()} to play");
        //    if (nextPlayer.GetComponent<Player>().aiDriven)
        //    {
        //        StartCoroutine(GiveTurnToAI(nextPlayer.GetComponent<Player>()));
        //        return;
        //    }
        //    nextPlayer.GetComponent<Player>().GetTurnAndCheckCards();
        //}
        // No need to explicitly order clients to play, they should know to give themselves a turn when it's due
    }
    //private IEnumerator GiveTurnToAI(Player player)
    //{
    //    yield return new WaitForSeconds(1f);
    //    player.GetTurnAndCheckCards();
    //}
    public IEnumerator StartNewGame()
    {
        yield return new WaitForSeconds(0.5f);
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
    private void SetNewHands()
    {
        for (int i = 0; i < clientsNewHands.Count; i++)
        {
            gameController.GetIPlayerByIndex(clientsNewHands[i].Item1).SetDeck(clientsNewHands[i].Item2);
            SendMessageToClient(connectedClients[i].Item1, $"setdeck;{clientsNewHands[i].Item1};{clientsNewHands[i].Item2}");
        }
        clientsNewHands.Clear();
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
    public void PlayedSeven(string chosenPlayerIndex)
    {
        clientsNewHands.Add((chosenPlayerIndex, GetStringRepresentationFromDeck(new(gameController.selfPlayer.GetDeck()))));
        if (gameController.GetPlayerObjectByIndex(chosenPlayerIndex).GetComponent<Player>()) // ai
        {
            Player aiPlayer = gameController.GetPlayerObjectByIndex(chosenPlayerIndex).GetComponent<Player>();
            
            clientsNewHands.Add((gameController.selfPlayer.GetIndex(), GetStringRepresentationFromDeck(new(aiPlayer.GetDeck()))));
            SetNewHands();
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
    public void DrawedCard(string index)
    {
        InformClients($"drawedcard;{index}");
    }
    public void CallOut()
    {
        InformClients($"calledout;{gameController.selfPlayer.GetIndex()}");
        gameController.CalledOut();
    }
    public void CallUno()
    {
        InformClients($"unoed;{gameController.selfPlayer.GetIndex()}");
        gameController.selfPlayer.UnshowCallOutButton();
    }
    private IEnumerator InformClientsLater(string message)
    {
        yield return new WaitForSeconds(1f);
        InformClients(message);
    }
    private IEnumerator TryRegisterPlayerDraw(string drawingPlayerIndex)
    {
        yield return new WaitForSeconds(1.1f);
        gameController.GetIPlayerByIndex(drawingPlayerIndex).DrawCard();
    }
}