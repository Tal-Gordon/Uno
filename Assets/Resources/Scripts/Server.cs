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
    public string serverName;
    public bool passwordLocked;

    private Socket multicastServer;
    private Socket server;
    private Thread receiveThread;

    [SerializeField] List<Socket> connectedClients = new();
    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;
    private readonly ushort defaultPort = 8888;
    private ushort userPort;
    private int numOfConnected = 1;
    private string localIP;

    void Start()
    {
        // Trick to find local IP address
        // Connecting a UDP socket and reading it's local endpoint
        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();
        }
        localIP = "127.0.0.1"; // Remove later

        multicastServer = SetupMulticastSocket();
        server = SetupServer();
        
        InvokeRepeating(nameof(SendMessageToMulticastGroup), 0f, 3f);
    }

    private Socket SetupServer()
    {
        IPAddress ipAddress = IPAddress.Parse(localIP);
        IPEndPoint localEndPoint = new(ipAddress, defaultPort);

        Socket server = new(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        server.Bind(localEndPoint);
        server.Listen(3);

        server.BeginAccept(
            (ar) =>
            {
                Socket listener = (Socket)ar.AsyncState;
                Socket client = listener.EndAccept(ar);

                connectedClients.Add(client);
                Thread clientThread = new(() => { HandleClient(client); });
                clientThread.Start();
            }
        , server);

        return server;
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

    public void SendMessageToMulticastGroup()
    {
        byte[] buffer = new byte[1024];
        string msg = string.Empty;

        msg = $"{localIP};{defaultPort};{serverName};{numOfConnected};{passwordLocked}";
        // Servers' own IP address, server name, number of already connected clients, password requirement
        // Delimiter is ;
        buffer = Encoding.Unicode.GetBytes(msg);

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
    }

    // Thread to continuously receive data
    private void HandleClient(Socket client)
    {
        while (true)
        {
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead = client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                string receivedMessage = Encoding.Unicode.GetString(buffer, 0, bytesRead).Trim();

                Array.Clear(buffer, 0, buffer.Length);

                try
                {
                    string[] message = receivedMessage.Split(";");

                    string clientIP = message[0];
                    string clientName = message[1];

                    // Process message logic

                    string messageToSend = "Verified";

                    IPEndPoint remoteEndPoint = new(IPAddress.Parse(clientIP), defaultPort);

                    buffer = Encoding.Unicode.GetBytes(messageToSend);
                    int bytesSent = client.Send(buffer, 0, buffer.Length, SocketFlags.None);
                }
                catch (Exception e)
                {
                    Debug.Log(e); // Client likely disconnected
                    if (receivedMessage == string.Empty) { client.Close(); break; }
                }
            }
            catch
            {
                break;
            }
        }
    }

    private string ProcessClientData(string data) // Incomplete, not in use yet
    {
        string[] dataArr = data.Split(";");
        IPAddress address;
        if (IPAddress.TryParse(dataArr[0], out address))
        {
            string clientName = dataArr[1];

            return "Verified";
        }
        else
        {
            return "Rejected";
        }
    }
    private void OnDisable()
    {
        CancelInvoke();
        multicastServer.Close();
        server.Close();
    }
}
