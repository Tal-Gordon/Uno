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
    public List<(string IpAddress, ushort port, string serverName, string numOfConnected, bool passReq)> serversInfo = new();
    public string clientName;

    private Socket multicastClient;
    private Socket client;
    private List<string> clientReceivedMessages = new();
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
                lock (clientReceivedMessages)
                {
                    clientReceivedMessages.Add(receivedMessage);
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
        lock (clientReceivedMessages)
        {
            foreach (string receivedMessage in clientReceivedMessages)
            {
                try
                {
                    string[] message = receivedMessage.Split(';');

                    string serverIpAddress = message[0];
                    ushort serverPort = ushort.Parse(message[1]);
                    string serverName = message[2];
                    string numOfConnected = message[3];
                    bool passReq = bool.Parse(message[4]);

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
                    // Remove rendered unavailable servers
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
            clientReceivedMessages.Clear();
        }
    }

    private void UpdateRenderedServers()
    {
        joinMenuController.RerenderServers();
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

        if (client == null) { return; }

        // Trick to find local IP address
        // Connecting a UDP socket and reading it's local endpoint
        string localIP;
        using (Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();
        }
        //localIP = "127.0.0.1"; // Remove later

        string messageToSend = $"{localIP};{clientName}";
        if (password) { messageToSend += ";something"; } // TODO

        byte[] buffer = new byte[1024];
        buffer = Encoding.Unicode.GetBytes(messageToSend);

        if (client.Connected)
        {
            client.BeginSend(new List<ArraySegment<byte>> { new ArraySegment<byte>(buffer) }, SocketFlags.None,
                (ar) =>
                {
                    int bytesSent = client.EndSend(ar);
                    Array.Clear(buffer, 0, buffer.Length);
                }
                , null);

            client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, 
                (ar) =>
                {
                    try
                    {
                        int bytesRead = client.EndReceive(ar);

                        string receivedString = Encoding.Unicode.GetString(buffer, 0, bytesRead);

                        Debug.Log($"Server sent: {receivedString}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                }, null);
        }
        else
        {
            Debug.LogError("Client not connected");
        }
    }

    public List<(string, string, bool)> GetServersInfo()
    {
        List<(string, string, bool)> toReturn = new();
        for (int i = 0; i < serversInfo.Count; i++)
        {
            toReturn.Add((serversInfo[i].serverName, serversInfo[i].numOfConnected, serversInfo[i].passReq));
        }
        return toReturn;
    }
    private void OnDisable()
    {
        if (client != null) { CancelInvoke(); client.Close(); }
    }
}
