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

public class Client : MonoBehaviour
{
    public static Client Instance;
    public JoinMenuController joinMenuController;

    public List<(string IpAddress, ushort port, string serverName, string numOfConnected, bool passReq, bool isAlive)> serversInfo = new();

    private Socket multicastClient;
    private Socket client;
    private Thread multicastReceiveThread;
    private Thread serverReceiveThread;

    private List<string> receivedMessages = new();
    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;

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
        // Set up the client socket
        multicastClient = SetupMulticastClient();

        // Start the thread to receive data
        multicastReceiveThread = new Thread(() => SocketReceiveThread(multicastClient));
        multicastReceiveThread.Start();

        StartCoroutine(ProcessReceivedMulticastData());
        StartCoroutine(ProcessReceivedServerData());
        StartCoroutine(CheckAliveServers());
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
            }
        }
    }

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
                    string messageToSend = GetResponse(receivedMessage); // Message process

                    if (messageToSend != string.Empty)
                    {
                        SendMessageToServer(messageToSend);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e); // Likely connection closed by server
                    if (receivedMessage == string.Empty) { socket.Close(); break; }
                }
            }
            receivedMessages.Clear();
        }
    }

    private IEnumerator ProcessReceivedMulticastData()
    {
        while (true)
        {
            ProcessSocketReceivedData(multicastClient);

            yield return new WaitForSeconds(1f);

            // TODO: stop function when connected to server, and restart if disconnected
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

    public void AskToJoin(string serverName, bool password = false)
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
        if (password) { messageToSend += ";something"; } // TODO

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
                            //if (serversInfo[i].port != serverPort) // Update the port, if needed
                            //{
                            //    serversInfo[i] = (serversInfo[i].IpAddress, serverPort, serversInfo[i].serverName, numOfConnected, serversInfo[i].passReq, serversInfo[i].isAlive);
                            //}

                            // Set isAlive property as true
                            serversInfo[i] = (serversInfo[i].IpAddress, serversInfo[i].port, serversInfo[i].serverName, numOfConnected, serversInfo[i].passReq, true);
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
                joinMenuController.DisconnectFromServer();

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

                RenderJoinedServer(hostUsername, player1Username, player2Username, player3Username);

                return string.Empty;
            }
            case "gamestart":
            {
                PrepareForGame();
                return string.Empty;
            }
            default:
            {
                // Handle unknown commands
                Debug.LogError($"Server sent unknown command: {command}");
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
        }
    }

    private void UpdateRenderedServers()
    {
        joinMenuController.RerenderServers();
    }

    private void PrepareForGame()
    {
        SceneManager.LoadScene("Game");
    }
}