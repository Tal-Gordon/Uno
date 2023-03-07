using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using TMPro;
using System.Threading;

public class Server : MonoBehaviour
{
    public TMP_InputField console;

    public string serverName;
    public bool passwordLocked;

    private Socket multicastServer;
    private Socket server;
    private Thread receiveThread;

    private List<string> receivedMessages = new();
    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;
    private readonly ushort defaultPort = 7777;
    private ushort userPort;
    private int numOfConnected = 1;

    void Start()
    {
        multicastServer = SetupMulticastSocket();
        server = SetupSocket();

        // Start the thread to receive data
        receiveThread = new Thread(() => ReceiveData(server));
        receiveThread.Start();
        InvokeRepeating(nameof(SendMessageToMulticastGroup), 0f, 0.1f);
        InvokeRepeating(nameof(ProcessReceivedMessages), 0f, 0.1f);
    }

    private Socket SetupSocket()
    {
        IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress ipAddress = ipHost.AddressList[0];
        IPEndPoint localEndPoint = new(ipAddress, defaultPort);

        Socket server = new(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        server.Bind(localEndPoint);
        server.Listen(3);

        return server;
    }

    private Socket SetupMulticastSocket()
    {
        // Declare new socket
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Multicast IP-address
        IPAddress ipAddress = IPAddress.Parse(multicastAddress);

        // Join multicast group
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(ipAddress));

        // TTL (Time to live)
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);

        // Create an endpoint
        IPEndPoint ipep = new(ipAddress, multicastPort);

        // Connect to the endpoint
        socket.Connect(ipep);

        // Return socket
        return socket;
    }

    public void SendMessageToMulticastGroup()
    {
        // Scan message
        byte[] buffer = new byte[1024];
        string msg = string.Empty;
        if (console != null) { msg = console.text.Trim(); }
        else
        {
            // Trick to find local IP address
            // Connecting a UDP socket and reading it's local endpoint
            string localIP;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }

            msg = $"{localIP};{defaultPort};{serverName};{numOfConnected};{passwordLocked}";
            // Servers' own IP address, server name, number of already connected clients, password requirement
            // Delimiter is ;
        }
        buffer = Encoding.Unicode.GetBytes(msg);

        // Send message
        multicastServer.BeginSend(new List<ArraySegment<byte>> { new ArraySegment<byte>(buffer) }, SocketFlags.None,
            (ar) =>
            {
                int bytesSent = multicastServer.EndSend(ar);
                if (msg.Equals("Bye!", StringComparison.Ordinal))
                {
                    try
                    {
                        multicastServer.Shutdown(SocketShutdown.Both);
                    }
                    finally
                    {
                        multicastServer.Close();
                    }
                }
            }
            , null);
    }

    // Thread to continuously receive data
    private void ReceiveData(Socket client)
    {
        while (true)
        {
            try
            {
                server.AcceptAsync();
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

    // Continuously process received data
    private void ProcessReceivedMessages()
    {
        lock (receivedMessages)
        {
            foreach (string receivedMessage in receivedMessages)
            {
                Debug.Log($"Received: {receivedMessage}");

                try
                {
                    // Process message logic
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }
            receivedMessages.Clear();
        }
    }
}
