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

public class Client : MonoBehaviour
{
    public List<(string IpAddress, ushort port, string serverName)> serversInfo = new();
    public string clientName;

    private Socket multicastClient;
    private Socket client;
    private List<string> receivedMessages = new();
    private Thread multicastReceiveThread;
    private JoinMenuController joinMenuController;

    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;
    private readonly string terminationCommand = "Bye!";

    void Start()
    {
        // Set up the client socket
        multicastClient = SetupMulticastClient();

        // Start the thread to receive data
        multicastReceiveThread = new Thread(() => ReceiveDataOnMulticast(multicastClient));
        multicastReceiveThread.Start();
        InvokeRepeating(nameof(ProcessReceivedMulticastMessages), 0f, 0.1f);
        // Maybe TODO: Add a refresh button that would trigger ProcessReceivedMulticastMessages instead of InvokeRepeating

        joinMenuController = GetComponent<JoinMenuController>();
    }

    void Update()
    {

    }

    private Socket SetupMulticastClient()
    {
        // Create new socket
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Create IP endpoint
        IPEndPoint ipep = new(IPAddress.Any, multicastPort);

        // Bind endpoint to the socket
        socket.Bind(ipep);

        // Multicast IP-address
        IPAddress ipAddress = IPAddress.Parse(multicastAddress);

        // Add socket to the multicast group
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(ipAddress, IPAddress.Any));

        return socket;
    }
    private Socket SetupClient(string ipAddress, ushort port)
    {
        IPAddress ipAddressToConnect = IPAddress.Parse(ipAddress);
        IPEndPoint localEndPoint = new(ipAddressToConnect, port);

        Socket client = new(ipAddressToConnect.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        client.Connect(localEndPoint);

        return client;
    }
    // Thread to continuously receive data on multicast group
    private void ReceiveDataOnMulticast(Socket client)
    {
        while (true)
        {
            try
            {
                byte[] data = new byte[1024];
                int bytesRead = client.Receive(data, 0, data.Length, SocketFlags.None);
                string str = Encoding.Unicode.GetString(data, 0, bytesRead);
                string receivedMessage = str.Trim();
                lock (receivedMessages)
                {
                    receivedMessages.Add(receivedMessage);
                }
            }
            catch
            {
                break;
            }
        }
    }

    // Continuously process received multicast data
    private void ProcessReceivedMulticastMessages()
    {
        lock (receivedMessages)
        {
            foreach (string receivedMessage in receivedMessages)
            {
                try
                {
                    string[] message = receivedMessage.Split(';');

                    string serverIpAddress = message[0];
                    ushort serverPort = ushort.Parse(message[1]);
                    string serverName = message[2];
                    string numOfConnected = message[3];
                    bool passwordBool = bool.Parse(message[4]);

                    bool serverExists = false;
                    for (int i = 0; i < serversInfo.Count; i++)
                    {
                        if (serversInfo[i].IpAddress == serverIpAddress) 
                        { 
                            serverExists = true; 
                            if (serversInfo[i].port != serverPort) { serversInfo[i] = (serversInfo[i].IpAddress, serverPort, serversInfo[i].serverName); }
                            break;
                        }
                    }
                    if (!serverExists) 
                    { 
                        serversInfo.Add((serverIpAddress, serverPort, serverName)); 
                        AddServerToGameView(serverName, numOfConnected, passwordBool);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }

                //if (receivedMessage.Equals(terminationCommand, StringComparison.Ordinal))
                //{
                //    Debug.Log("termination command received");
                //    client.Close();
                //    receiveThread.Join();
                //    CancelInvoke(nameof(ProcessReceivedMulticastMessages)); break;
                //}
            }
            receivedMessages.Clear();
        }
    }

    private void AddServerToGameView(string serverName, string amountOfConnected, bool passwordRequirement = false)
    {
        joinMenuController.AddServer(serverName, amountOfConnected, passwordRequirement);
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

        Socket client = SetupClient(ipAddress, port);

        // Trick to find local IP address
        // Connecting a UDP socket and reading it's local endpoint
        string localIP;
        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();
        }

        string messageToSend = $"{localIP};{clientName}";
        if (password) { messageToSend += ";something"; } // TODO
        Debug.LogError($"Client sent: {messageToSend}");

        byte[] buffer = new byte[1024];
        buffer = Encoding.Unicode.GetBytes(messageToSend);

        if (client.Connected)
        {
            client.BeginSend(new List<ArraySegment<byte>> { new ArraySegment<byte>(buffer) }, SocketFlags.None,
            (ar) =>
            {
                int bytesSent = client.EndSend(ar);

            }
            , null);

            byte[] messageReceived = new byte[1024];
            int byteReceived = client.Receive(messageReceived);

            string actualMessageReceived = Encoding.Unicode.GetString(messageReceived, 0, byteReceived);
            Debug.LogError($"Message from Server: {actualMessageReceived}");
        }
        else
        {
            Debug.LogError("Client not connected");
        }
    }
}
