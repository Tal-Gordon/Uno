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

public class Server : MonoBehaviour
{
    public static Server Instance;
    public HostMenuController hostMenuController;

    public string serverName;
    public bool passwordLocked;
    public string serverPassword;

    private Socket multicastServer;
    private Socket server;

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
    void Start()
    {
        
    }
    public void ManualStart()
    {
        // Trick to find local IP address
        // Connecting a UDP socket and reading it's local endpoint
        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
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
                    //if (msg.Equals("Bye!", StringComparison.Ordinal))
                    //{
                    //    try
                    //    {
                    //        multicastServer.Shutdown(SocketShutdown.Both);
                    //    }
                    //    finally
                    //    {
                    //        multicastServer.Close();
                    //    }
                    //}
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

    private void DisconnectClient(Socket client)
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
    }

    public void StartGame()
    {
        InformClients("gamestart");
        SceneManager.LoadScene("Game");
    }
}