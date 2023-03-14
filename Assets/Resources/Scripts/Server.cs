using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using TMPro;
using System.Threading;
using UnityEditor.PackageManager;
using System.Linq;

public class Server : MonoBehaviour
{
    public string serverName;
    public bool passwordLocked;

    private Socket multicastServer;
    private Socket server;
    private Thread receiveThread;
    private HostMenuController hostMenuController;

    private List<(string, string)> connectedClients = new(); // IP address, username
    private List<(Socket, string)> receivedMessages = new(); // Socket that sent message, message
    private string[] connectedUsernames = new string[3];
    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;
    private readonly ushort defaultPort = 8888;
    private ushort userPort;
    private int numOfConnected = 1;
    private string localIP;

    void Start()
    {
        hostMenuController = GetComponent<HostMenuController>();
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

        StartCoroutine(SendMessageToMulticastGroup());
        StartCoroutine(ProcessReceivedData());
    }
    private void Update()
    {
        connectedUsernames = hostMenuController.GetPlayersUsernames();
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

                Thread clientThread = new(() => { SocketReceiveThread(client); });
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

    public IEnumerator SendMessageToMulticastGroup()
    {
        byte[] buffer = new byte[8192];
        string msg = $"multicast;{localIP};{defaultPort};{serverName};{numOfConnected};{passwordLocked}";
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

    // Thread to continuously receive data from a specific socket and write it to receivedMessages
    private void SocketReceiveThread(Socket socket)
    {
        byte[] buffer = new byte[8192];
        while (true)
        {
            int bytesRead = socket.Receive(buffer);
            if (bytesRead > 0)
            {
                string receivedMessage = Encoding.Unicode.GetString(buffer, 0, bytesRead).Trim();

                lock (receivedMessages)
                {
                    receivedMessages.Add((socket, receivedMessage));
                }
            }
            else
            {
                // Client rude disconnect
                break;
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
                        string messageToSend = GetResponse(receivedMessage); // Message process

                        if (messageToSend != string.Empty)
                        {
                            SendMessageToClient(sendingSocket ,messageToSend);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e); // Client likely disconnected, we want to remove it from out list of connected clients
                        if (receivedMessage == string.Empty) 
                        {
                            IPEndPoint remoteEndPoint = sendingSocket.RemoteEndPoint as IPEndPoint;
                            if (remoteEndPoint != null) 
                            {
                                // The remoteEndPoint trick is used to find the IP address of the connected socket, so we can remove it from our list
                                string clientIPAddress = remoteEndPoint.Address.ToString();
                                // We iterate over the list, and search for the client with the ip address we found earlier
                                // Once found, we use his username to remove it from our game view, and then remove him from our internal list
                                foreach (var client in connectedClients)
                                {
                                    if (client.Item1 == clientIPAddress)
                                    {
                                        hostMenuController.DisconnectClient(client.Item2);
                                        connectedClients.Remove(client);
                                        break;
                                    }
                                }
                            }
                        }
                        sendingSocket.Close();
                        break;
                    }
                }
                receivedMessages.Clear();
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private string GetResponse(string data)
    {
        string[] parts = data.Split(';');
        string command = parts[0];

        switch (command)
        {
            case "connect":

                string clientIP = parts[1];
                string clientName = parts[2];
                if (parts.Length > 3) { string password = parts[3]; }
                // TODO: verify password

                if (connectedClients.Count < 3)
                {
                    connectedClients.Add((clientIP, clientName));
                    hostMenuController.ConnectNewClient(clientName);
                    return $"ok;{connectedUsernames[0]};{connectedUsernames[1]};{connectedUsernames[2]}";
                }
                else { return "full"; }

            default:
                // Handle unknown commands
                return "unknown";
        }
    }

    private void OnDisable()
    {
        CancelInvoke();
        multicastServer.Close();
        server.Close();
    }
}
