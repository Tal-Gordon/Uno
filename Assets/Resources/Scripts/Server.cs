using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using TMPro;

public class Server : MonoBehaviour
{
    private Socket multicastServer;

    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;
    private readonly ushort defaultPort = 7777;
    private ushort userPort;

    public TMP_InputField console;

    void Start()
    {
        multicastServer = SetupMulticastSocket();
    }

    private Socket SetupSocket()
    {
        IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress ipAddress = ipHost.AddressList[0];
        IPEndPoint localEndPoint = new(ipAddress, defaultPort);

        Socket server = new(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

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
        string msg = console.text.Trim();
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
}
