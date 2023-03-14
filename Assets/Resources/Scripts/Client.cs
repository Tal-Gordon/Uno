using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine.SceneManagement;
using System.Linq;

public class Client : MonoBehaviour
{
    public List<(string IpAddress, ushort port, string serverName, string numOfConnected, bool passReq)> serversInfo = new();
    public string clientUsername;

    private Socket multicastClient;
    private Socket client;
    private Thread multicastReceiveThread;
    private Thread serverReceiveThread;
    private JoinMenuController joinMenuController;

    private List<string> receivedMessages = new();
    private string localIP;
    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;

    void Start()
    {
        // Set up the client socket
        multicastClient = SetupMulticastClient();

        // Start the thread to receive data
        multicastReceiveThread = new Thread(() => SocketReceiveThread(multicastClient));
        multicastReceiveThread.Start();

        StartCoroutine(ProcessReceivedMulticastData());

        joinMenuController = GetComponent<JoinMenuController>();

        // Trick to find local IP address
        // Connecting a UDP socket and reading it's local endpoint
        using (Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();
        }
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
        IPEndPoint localEndPoint = new(ipAddressToConnect, port);

        Socket client = new(ipAddressToConnect.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            var connectResult = client.BeginConnect(localEndPoint, null, null);

            // Wait for the connection to complete asynchronously
            bool isConnected = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5), true);

            if (isConnected)
            {
                client.EndConnect(connectResult);
            }
            else
            {
                Debug.Log("Connection timed out");
                client.Close();
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
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
                Debug.Log(e); 
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
                    Debug.Log(e); // Likely connection closed by server
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
            yield return new WaitForSeconds(1f);

            ProcessSocketReceivedData(multicastClient);

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

                yield return new WaitForSeconds(2f);

                // TODO: stop function if disconnected from server, and restart when connected
            }
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
        StartCoroutine(ProcessReceivedServerData());

        string messageToSend = $"connect;{localIP};{clientUsername}";
        // command identifier "connect", client's ip address, username
        if (password) { messageToSend += ";something"; } // TODO

        SendMessageToServer(messageToSend);

        // Start the thread to receive data
        serverReceiveThread = new Thread(() => SocketReceiveThread(client));
        serverReceiveThread.Start();
    }

    public void DisconnectFromServer()
    {
        if (client.Connected) { SendMessageToServer($"disconnect;{localIP}"); }
        StopCoroutine(ProcessReceivedServerData());
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
                string hostUsername = parts[1];
                string player1Username = parts[2];
                string player2Username = parts[3];

                RenderJoinedServer(hostUsername, player1Username, player2Username);

                return string.Empty;

            case "multicast":
                string serverIpAddress = parts[1];
                ushort serverPort = ushort.Parse(parts[2]);
                string serverName = parts[3];
                string numOfConnected = parts[4];
                bool passReq = bool.Parse(parts[5]);

                bool serverExists = false;
                for (int i = 0; i < serversInfo.Count; i++)
                {
                    if (serversInfo[i].IpAddress == serverIpAddress)
                    {
                        serverExists = true;
                        if (serversInfo[i].port != serverPort) { serversInfo[i] = (serversInfo[i].IpAddress, serverPort, serversInfo[i].serverName, numOfConnected, serversInfo[i].passReq); }
                        break;
                    }
                }

                if (!serverExists)
                {
                    serversInfo.Add((serverIpAddress, serverPort, serverName, numOfConnected, passReq));
                    UpdateRenderedServers();
                }

                //List<string> tempServerNames = joinMenuController.GetServersNameList();
                //for (int i = 0; i < serversInfo.Count; i++)
                //{
                //    if (!tempServerNames.Contains(serversInfo[i].serverName))
                //    {
                //        serversInfo.RemoveAt(i);
                //    }
                //}

                // TODO: Remove rendered unavailable servers

                return string.Empty;

            default:
                // Handle unknown commands
                return "unknown";
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

    public void RenderJoinedServer(string hostUsername, string player1Username, string player2Username)
    {
        joinMenuController.RenderJoinedServer(hostUsername, player1Username, player2Username, clientUsername);
    }
    private void OnDisable()
    {
        if (client != null) { CancelInvoke(); client.Close(); }
    }

    private void UpdateRenderedServers()
    {
        joinMenuController.RerenderServers();
    }

}