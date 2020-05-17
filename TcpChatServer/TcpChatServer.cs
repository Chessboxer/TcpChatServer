﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpChatServer
{
    class TcpChatServer
    {

        // What listens in 
        private TcpListener _listener;

        // Clients connected
        private List<TcpClient> _clients = new List<TcpClient>();

        // Names that are taken by other messengers
        private Dictionary<TcpClient, string> _names = new Dictionary<TcpClient, string>();

        private Queue<string> _messageQueue = new Queue<string>();

        // Extra fun data
        public readonly string ChatName;
        public readonly int Port;
        public bool Running { get; private set; }

        // Buffer 
        public readonly int BufferSize = 2 * 1024; //KB

        public TcpChatServer(string chatName, int port)
        {
            // Set the basic data
            ChatName = chatName;
            Port = port;
            Running = false;

            // make listener listen for connections on any network device
            _listener = new TcpListener(IPAddress.Any, Port);
        }

        // If the server is running, this will shutdown the server

        public void Shutdown()
        {
            Running = false;
            Console.WriteLine("Shutting downt he server");
        }

        //  Start runnign the server. Will; stop when Shutdown() has been called

        public void Run()
        {
            // some info
            Console.WriteLine($"Starting the \"{ChatName} TCP Chat Server\" on port {Port}");
            Console.WriteLine("Press Ctrl-C to shutdown the server at any time.");

            // Make the server run
            _listener.Start();
            Running = true;

            // Main server loop
            while (Running)
            {
                // Check for new clients
                if (_listener.Pending())
                    _handleNewConnection();

                // Do the rest
                _checkForDisconnects();
                _checkForNewMessages();
                _sendMessages();

                // Use less CPU
                Thread.Sleep(10);
            }

            foreach (TcpClient client in _clients)
                _cleanupClient(client);

            _listener.Stop();

            //Some info 
            Console.WriteLine("Sever is shutdown");

        }

        private void _handleNewConnection()
        {
            // There is (at least) one, see what they want
            bool good = false;
            TcpClient newClient = _listener.AcceptTcpClient(); // Blocks
            NetworkStream netStream = newClient.GetStream();

            // Modify the default buffer sizes
            newClient.SendBufferSize = BufferSize;
            newClient.ReceiveBufferSize = BufferSize;

            // Print some info
            EndPoint endPoint = newClient.Client.RemoteEndPoint;
            Console.WriteLine($"Handling a new client from {endPoint}...");

            // Let them identify themselves
            byte[] msgBuffer = new byte[BufferSize];
            int bytesRead = netStream.Read(msgBuffer, 0, msgBuffer.Length); // Blocks
            // Console.WriteLine($"Got {bytesRead} bytes.");
            if (bytesRead > 0)
            {
                string msg = Encoding.UTF8.GetString(msgBuffer, 0, bytesRead);

                // Okay, so they might be a messenger
                string name = msg.Substring(msg.IndexOf(':') + 1);

                    if ((name != string.Empty) && (!_names.ContainsValue(name)))
                    {
                        good = true;
                        _names.Add(newClient, name);
                        _clients.Add(newClient);
                    }

                    Console.Write($"{endPoint} is a new messenger with the name {name}.");
                // Tells the viewers we have a new messenger
                    _messageQueue.Enqueue(String.Format($"{name} has entered the chat."));
                                             
            }
            // Do we really want them?
            if (!good)
                newClient.Close();
        }

        // Sees if any of the clients have left the chat server
        private void _checkForDisconnects()
        {

            // Check the clients
            foreach (TcpClient client in _clients.ToArray()){
                if (_isDisconnected(client))
                {
                    // get name
                    string name = _names[client];

                    // inform 
                    Console.Write($"Messenger {name} has left.");
                    _messageQueue.Enqueue(String.Format($"{name} has left the chat."));

                    // clean up
                    _clients.Remove(client); // Remove from list of clients
                    _names.Remove(client); // remove name from dictionary 
                    _cleanupClient(client); // close tcpclient steram and object
                }
            }
        }

        //  See if any of our messengers have sent us a new message, put it in the queue
        private void _checkForNewMessages()
        {
            foreach (TcpClient client in _clients)
            {
                int messageLength = client.Available;
                if (messageLength > 0)
                {
                    // There is one! Get it.
                    byte[] msgBuffer = new byte[messageLength];
                    client.GetStream().Read(msgBuffer, 0, msgBuffer.Length);

                    // Attach a name to it and shove it into the queue
                    string msg = String.Format($"{_names[client]}: {Encoding.UTF8.GetString(msgBuffer)}");
                    _messageQueue.Enqueue(msg);
                }
            }
        }

        // Clears out the message queue and sends it to all of the viewers
        private void _sendMessages()
        {
            foreach (string msg in _messageQueue)
            {
                Console.WriteLine($"{msg}");
                // Encode the message
                byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);

                // send the message to each client
                foreach (TcpClient client in _clients)
                    client.GetStream().Write(msgBuffer, 0, msgBuffer.Length);

                //// send the message to each viewer
                //foreach (TcpClient v in _viewers)
                //    v.GetStream().Write(msgBuffer, 0, msgBuffer.Length); // BLocks
            }
            //clear out the queue
            _messageQueue.Clear();

        }
        // Checks if a socket has disconnected
        // adapted from http://stackoverflow.com/questions/722240/instantly-detect-client-disconnection-from-server-socket
        private bool _isDisconnected(TcpClient client)
        {
            try
            {
                Socket s = client.Client;
                return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
            }
            catch (SocketException se)
            {
                // We got a socket error, assume its disconnected
                return true;
            }
        }

        // cleans up resources for TcpCLient
        private void _cleanupClient(TcpClient client)
        {
            client.GetStream().Close(); // close network stream
            client.Close();             // Close client
        }

        public static TcpChatServer chat;

        protected static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
        {
            chat.Shutdown();
            args.Cancel = true;
        }

        public static void Main(string[] args)
        {
            // Create the server
            string name = "Chessboxer's Realm"; // args[0].Trim();
            int port = 6000; //int.Parse(args[1].Trim());
            chat = new TcpChatServer(name, port);

            //  Add a handler for a ctrl-C press
            Console.CancelKeyPress += InterruptHandler;

            //run the chat server
            chat.Run();
        }
    }

}