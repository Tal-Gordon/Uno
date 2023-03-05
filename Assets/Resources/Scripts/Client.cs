using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System;
using System.Text;
using System.Threading;
using TMPro;
using Unity.VisualScripting;

public class Client : MonoBehaviour
{
    public TextMeshProUGUI consoleReceiver;

    private Socket client;
    private List<string> receivedMessages = new();
    private Thread receiveThread;

    private readonly string multicastAddress = "239.255.42.99";
    private readonly ushort multicastPort = 15000;
    private readonly string terminationCommand = "Bye!";

    void Start()
    {
        // Set up the client socket
        client = SetupMulticastClient();

        // Start the thread to receive data
        receiveThread = new Thread(() => ReceiveData(client));
        receiveThread.Start();
        InvokeRepeating(nameof(ProcessReceivedMessages), 0f, 0.1f);   
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

    // Thread to continuously receive data
    private void ReceiveData(Socket client)
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

    // Continuously process received data
    private void ProcessReceivedMessages()
    {
        lock (receivedMessages)
        {
            foreach (string receivedMessage in receivedMessages)
            {
                consoleReceiver.text = $"Client received: '{receivedMessage}'";
                if (receivedMessage.Equals(terminationCommand, StringComparison.Ordinal))
                {
                    Debug.Log("termination command received");
                    client.Close();
                    receiveThread.Join();
                    CancelInvoke(nameof(ProcessReceivedMessages)); break;
                }
            }
            receivedMessages.Clear();
        }
    }
}
