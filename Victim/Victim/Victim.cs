using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Victim
{
    class VictimClass
    {
        //Socket tcpListenSocket = new Socket(getLocalIpAdress().AddressFamily, SocketType.Stream, ProtocolType.Tcp);


        // Attack command: IP, 3000, pizzaa
        // Attack command: 192.168.233.1, 3000, pizzaa


        private int LISTENING_PORT = getFreeTCPListeningPort();
        //private readonly String MY_PASSWORD = "pizzaa";

        // Create random password
        private readonly String MY_PASSWORD = RandomString();
        private ArrayList hackTimeList = new ArrayList();
        private Mutex timeStampMutex = new Mutex();

        private const int numOfBotToCrash = 10;
        private const int SecondsThresholdForAttack = 1;


        public VictimClass()
        {
            // Listen to clients
            startListening();

        }

        private static string RandomString() 
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            Random random = new Random();
            return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
        }


        /// <summary>
        /// Gets a free port number.
        /// </summary>
        /// <returns></returns>
        private static int getFreeTCPListeningPort()
        {
            // Get all Active port numbers
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] udpIPEndPoint = ipGlobalProperties.GetActiveTcpConnections();

            int port = 0;
            bool isAvailable = false;
            Random rnd = new Random();
            // Get a random number and check if its free
            while (!isAvailable)
            {
                port = rnd.Next(3000, 4000);
                bool breakBool = false;
                foreach (TcpConnectionInformation udpIP in udpIPEndPoint)
                {
                    if (udpIP.RemoteEndPoint.Port == port)
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


        private void startListening()
        {
            // Setup the listener
            IPAddress localAdd = getLocalIpAdress();
            TcpListener listenerSocket = new TcpListener(IPAddress.Any, LISTENING_PORT);
            listenerSocket.Start();
            Console.WriteLine("Server is listening on IP:{0}, Port:{1}, password is {2}",localAdd,LISTENING_PORT,MY_PASSWORD);


            while (true)
            {

                // Connect to any client
                TcpClient newClient = listenerSocket.AcceptTcpClient();

                // Start signing the new client
                Thread signThread = new Thread(() => signInANewClient(newClient));
                signThread.Start();

            }

            // Not used
            listenerSocket.Stop();

        }

        private void signInANewClient(TcpClient newClient)
        {
            try
            {
                // Create stream with client
                NetworkStream nwStream = newClient.GetStream();


                // Send: Please enter your password\r\n
                byte[] bytesToSend = convertStringToBytes("Please enter your password\r\n");
                nwStream.Write(bytesToSend, 0, bytesToSend.Length);

                // Get password from client - check if valid
                byte[] passwordFromClient = new byte[newClient.ReceiveBufferSize];
                int passwordSize = nwStream.Read(passwordFromClient, 0, newClient.ReceiveBufferSize);

                // Password answer size = 6 Bytes
                byte[] passwordAnswer = getSubArray(passwordFromClient, 0, 6);

                if (passwordSize == 6)
                {

                    // Get password as String
                    string passwordString = convertBytesToString(passwordAnswer);
                    if (passwordString.Equals(MY_PASSWORD))
                    {

                        // Send: Access granted\r\n 
                        bytesToSend = convertStringToBytes("Access granted\r\n");
                        nwStream.Write(bytesToSend, 0, bytesToSend.Length);

                        // Get data from client
                        byte[] dataFromClinet = new byte[1024];
                        int dataSize = nwStream.Read(passwordFromClient, 0, newClient.ReceiveBufferSize);

                        if (dataSize != 44)
                            return;


                        // Size of : "Hacked by " = 10 Bytes
                        // Size of : CCServer = 32 Bytes
                        // Size of : "\r\n" = 2 Bytes
                        byte[] messageFromClient = getSubArray(passwordFromClient, 0, 44);

                        // Get data as String
                        string clientMessage = convertBytesToString(messageFromClient);

                        // Check if data is Hack command
                        if (clientMessage.StartsWith("Hacked by"))
                        {
                            // Check 

                            if (isAttacked())
                            {
                                // Reset timeStamps
                                Console.WriteLine(clientMessage);
                            }

                        }

                    }
                }
                nwStream.Close();
                newClient.Close();

            }
            catch(Exception ex)
            {

            }
            


        }

        public bool isAttacked()
        {
            timeStampMutex.WaitOne();
            DateTime currentTimeStamp = DateTime.Now;
            int addedIndex = hackTimeList.Add(currentTimeStamp);
            if (addedIndex + 1 >= numOfBotToCrash)
            {
                DateTime crucialTime = (DateTime)hackTimeList[addedIndex + 1 - numOfBotToCrash];
                if (crucialTime.AddSeconds(SecondsThresholdForAttack) >= currentTimeStamp)
                {

                    hackTimeList.Clear();
                    timeStampMutex.ReleaseMutex();

                    return true;
                }
                hackTimeList.RemoveRange(0, addedIndex + 2 - numOfBotToCrash);
            }
            timeStampMutex.ReleaseMutex();
            return false;
        }




        // ***      Generic methods      ***     

        private byte[] getSubArray(byte[] original, int start, int end)
        {
            byte[] subArray = new byte[end - start];
            for (int i = start; i < end; i++)
                subArray[i - start] = original[i];
            return subArray;
        }



        private String convertBytesToString(byte[] value)
        {
            return Encoding.ASCII.GetString(value);
        }

        private byte[] convertStringToBytes(String value)
        {
            return Encoding.ASCII.GetBytes(value);
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


        public class CustomComparer : IComparer
        {
            Comparer _comparer = new Comparer(System.Globalization.CultureInfo.CurrentCulture);

            public int Compare(object date1, object date2)
            {
                return DateTime.Compare((DateTime)date1, (DateTime)date2);
            }
        }


    }


}

