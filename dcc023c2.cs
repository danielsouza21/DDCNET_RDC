using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DCCNET_TP0
{
    public class dcc023c2
    {
        private static string SYNC_VALUE = "dcc023c2";
        private static string FLAG_ACK = "80";
        private static string FLAG_END = "40";
        private static string FLAG_INFO = "00";
        public static string FileOutput;

        static void Main(string[] args)
        {
            if ((args.Length < 4))
            {
                Console.WriteLine("Argumentos invalidos.");
                Console.WriteLine("<dcc023c2.exe> -s <port> <input> <output>");
                Console.WriteLine("<dcc023c2.exe> -c <IP> <port> <input> <output>");
            }

            if (args[0] == "-c")
            {
                FileOutput = args[4];
                // Cliente configure
                AsynchronousClient.StartClient(args[1], args[2]);
            }
            else if (args[0] == "-s")
            {
                FileOutput = args[3];
                // Server configure
                AsynchronousSocketServer.StartListening(args[1]);
            }
        }

        #region ConfConnection

        public class StateObject
        {
            // Client socket.  
            public Socket workSocket = null;
            // Size of receive buffer.  
            public const int BufferSize = 256;
            // Receive buffer.  
            public byte[] buffer = new byte[BufferSize];
            // Received data string.  
            public StringBuilder sb = new StringBuilder();
        }

        #region Cliente

        public static class AsynchronousClient
        {
            private static ManualResetEvent connectDone = new ManualResetEvent(false);
            private static ManualResetEvent sendDone = new ManualResetEvent(false);
            private static ManualResetEvent receiveDone = new ManualResetEvent(false);

            public static void StartClient(string ip, string port)
            {
                try
                {
                    // Establish the remote endpoint for the socket.
                    IPAddress ipAddress = IPAddress.Parse(ip);
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, int.Parse(port));

                    // Create a TCP/IP socket.  
                    Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    // Connect to the remote endpoint.  
                    client.BeginConnect(remoteEP,
                        new AsyncCallback(ConnectCallback), client);
                    connectDone.WaitOne();

                    MasterLoop.DCCNET(client, true);

                    // Release the socket.  
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            private static void ConnectCallback(IAsyncResult ar)
            {
                try
                {
                    // Retrieve the socket from the state object.  
                    Socket client = (Socket)ar.AsyncState;

                    // Complete the connection.  
                    client.EndConnect(ar);

                    Console.WriteLine("Socket connected to {0}",
                        client.RemoteEndPoint.ToString());

                    // Signal that the connection has been made.  
                    connectDone.Set();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            private static void ReceiveCallback(IAsyncResult ar)
            {
                try
                {
                    // Retrieve the state object and the client socket
                    // from the asynchronous state object.  
                    StateObject state = (StateObject)ar.AsyncState;
                    Socket client = state.workSocket;

                    // Read data from the remote device.  
                    int bytesRead = client.EndReceive(ar);

                    if (bytesRead > 0)
                    {
                        // There might be more data, so store the data received so far.  
                        state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                        // Get the rest of the data.  
                        client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                            new AsyncCallback(ReceiveCallback), state);
                    }
                    else
                    {
                        // All the data has arrived; put it in response.  
                        if (state.sb.Length > 1)
                        {
                            Console.WriteLine("[Client] Data Received: {0}", state.sb.ToString());
                        }
                        // Signal that all bytes have been received.  
                        receiveDone.Set();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        #endregion

        #region Server

        public static class AsynchronousSocketServer
        {
            // Thread signal.  
            public static ManualResetEvent allDone = new ManualResetEvent(false);

            public static void StartListening(string port)
            {
                // Establish the local endpoint for the socket.   
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.IPv6Any, int.Parse(port)); // Any IP

                // Create a TCP/IP socket.  
                Socket listener = new Socket(AddressFamily.InterNetworkV6,
                    SocketType.Stream, ProtocolType.Tcp);

                listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false); // Accept ipv4 and ipv6

                // Bind the socket to the local endpoint and listen for incoming connections.  
                try
                {
                    listener.Bind(localEndPoint);
                    listener.Listen(100);

                    while (true)
                    {
                        // Set the event to nonsignaled state.  
                        allDone.Reset();

                        // Start an asynchronous socket to listen for connections.  
                        Console.WriteLine("Waiting for a connection...");
                        listener.BeginAccept(
                            new AsyncCallback(AcceptCallback),
                            listener);

                        // Wait until a connection is made before continuing.  
                        allDone.WaitOne();
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                Console.WriteLine("\nPress ENTER to continue...");
                Console.Read();

            }

            private static void AcceptCallback(IAsyncResult ar)
            {

                // Get the socket that handles the client request.  
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                // Create the state object.  
                StateObject state = new StateObject();
                state.workSocket = handler;

                try
                {
                    MasterLoop.DCCNET(handler, false);
                }
                finally
                {
                    // Signal the main thread to continue.  
                    allDone.Set();
                    Console.WriteLine("Server connection end");
                }
            }
        }

        #endregion

        #endregion

        #region Utils Functions
        private static string SomaHexa(string valor1, string valor2)
        {
            uint valorInteiro1 = Convert.ToUInt32(valor1, 16);
            uint valorInteiro2 = Convert.ToUInt32(valor2, 16);
            var soma = valorInteiro1 + valorInteiro2;
            string complementoHex = string.Format("{0:X}", soma);

            if (complementoHex.Length > 4)
            {
                soma += 1;
                complementoHex = string.Format("{0:X}", soma);
            }

            complementoHex = complementoHex.PadLeft(4, '0');
            return complementoHex.Substring(complementoHex.Length - 4, 4);
        }

        public static class FrameworkSize
        {
            public static int Sync1_Size = sizeof(Int32);
            public static int Sync2_Size = sizeof(Int32);
            public static int Lenght = sizeof(Int16);
            public static int Checksum = sizeof(Int16);
            public static int Id = sizeof(Byte);
            public static int Flags = sizeof(Byte);

            public static List<int> Values = new List<int>
                {
                    Sync1_Size,
                    Sync2_Size,
                    Lenght,
                    Checksum,
                    Id,
                    Flags
                };
        }

        public class Frame
        {
            public string Sync1;
            public string Sync2;
            public string Lenght;
            public string Checksum;
            public string Id;
            public string Flags;
            public string Data;

            public Frame() { }

            public Frame(string checksum, string id, string flags, string data)
            {
                string dataConcat = string.Empty;

                foreach (var ch in data)
                {
                    dataConcat += "0";
                    dataConcat += ch;
                }

                var dataSize = dataConcat.Length.ToString().PadLeft(4, '0');

                if (dataSize.Length > 4)
                {
                    throw new Exception("Dado maior que o esperado.");
                }

                //Flags = calculaChecksum()

                Sync1 = SYNC_VALUE;
                Sync2 = SYNC_VALUE;
                Lenght = dataSize;
                Checksum = checksum;
                Id = id.PadLeft(2, '0');
                Flags = flags;
                Data = dataConcat;
            }

            public override string ToString()
            {
                return Sync1 + Sync2 + Lenght + Checksum + Id + Flags + Data;
            }
        }

        public static Frame ContentToFramework(string content)
        {
            var Frame = new Frame();
            var buildedString = string.Empty;
            Frame.Sync1 = content.Substring(0, FrameworkSize.Sync1_Size * 2);
            buildedString += Frame.Sync1;
            Frame.Sync2 = content.Substring(buildedString.Length, FrameworkSize.Sync2_Size * 2);
            buildedString += Frame.Sync2;
            Frame.Lenght = content.Substring(buildedString.Length, FrameworkSize.Lenght * 2);
            buildedString += Frame.Lenght;
            Frame.Checksum = content.Substring(buildedString.Length, FrameworkSize.Checksum * 2);
            buildedString += Frame.Checksum;
            Frame.Id = content.Substring(buildedString.Length, FrameworkSize.Id * 2);
            buildedString += Frame.Id;
            Frame.Flags = content.Substring(buildedString.Length, FrameworkSize.Flags * 2);
            buildedString += Frame.Flags;
            Frame.Data = content.Substring(buildedString.Length, Int16.Parse(Frame.Lenght));

            return Frame;
        }

        private static void WriteTxt(string content, string filename)
        {
            var file = $"{filename}.txt";
            File.AppendAllText(file, content + Environment.NewLine);
        }

        #endregion

        #region Generic Main Loop

        public static class MasterLoop
        {
            public static void DCCNET(Socket socket, bool client)
            {
                int data;
                int actualId = 0;
                int? lastId = null;
                bool nodeFinished = false;

                if (client)
                {
                    data = 11;
                }
                else
                {
                    data = 55;
                }

                while (true)
                {
                    if(!(data == 33 || data == 65)) // If does not exist data
                    {
                        actualId = actualId == 0 ? 1 : 0;
                        var framework = new Frame("faef", actualId.ToString(), FLAG_INFO, "1234" + data).ToString();

                        Console.WriteLine("Send Framework: " + framework);

                        byte[] byteData1 = Encoding.ASCII.GetBytes(framework);
                        socket.Send(byteData1);
                        //SendAsync(socket, framework);
                        //sendDone.WaitOne();
                    }
                    else
                    {
                        break;
                    }

                    while (true)
                    {
                        var buffer = new StateObject().buffer;
                        string content = string.Empty;
                        int bytesRec;
                        Frame frame;

                        while (true)
                        {
                            bytesRec = socket.Receive(buffer);
                            content += Encoding.ASCII.GetString(buffer, 0, bytesRec);
                            if ((bytesRec > 0) && (bytesRec < StateObject.BufferSize))
                            {
                                frame = ContentToFramework(content);
                                Console.WriteLine("Decode Received Framework: " + frame.ToString());
                                content = "";

                                break;
                            }
                        }

                        // If Verifica Checksum + Verifica Framework (SYNC1 + SYNC2)

                        if ((Int16.Parse(frame.Lenght) == 0) && (frame.Flags == FLAG_ACK)) // ACK
                        {
                            Console.WriteLine("[Frame ACK] Data: " + frame.ToString());
                            data++;
                            break;
                        }
                        else if ((Int16.Parse(frame.Lenght) == 0) && (frame.Flags == FLAG_END)) // End Communication
                        {

                        }
                        else // Information
                        {
                            if ((Int32.Parse(frame.Id) != lastId) || lastId == null)  // Accept just if is a different info
                            {
                                Console.WriteLine("Accepted Info");

                                var frameACK = new Frame("faef", frame.Id, FLAG_ACK, "").ToString();

                                WriteTxt(frame.ToString(), FileOutput);

                                //SendAsync(socket, frameACK);
                                //sendDone.WaitOne();
                                Console.WriteLine("Send confirmation: " + frameACK);
                                byte[] byteData = Encoding.ASCII.GetBytes(frameACK);
                                socket.Send(byteData);
                            }
                            else
                            {
                                Console.WriteLine("Same ID");
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}
