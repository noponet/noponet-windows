using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Proxy
{
    public class ProxyServer
    {

         string[] whiteList = { "github.com" };
        private string[] keywords = { "github" };
        private string[] localWhiteList;
        private string[] localKeywords;
        private string trollpage;
        private FeedServer feedServer;
        static void Main(string[] args)
        {
            ProxyServer p = new ProxyServer();
            p.run();

            Console.ReadLine();
        }
        public ProxyServer()
        {
            this.feedServer = new FeedServer(this);
        }
        public string[] LoadArrayFromFile(string path)
        {
            List<string> lines = new List<string>();
            try
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine("File not found at: " + path);
                    return null;
                }


                using (StreamReader reader = new StreamReader(path))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading file " + e.Message);
            }

            return lines.ToArray();
        }
        public void loadWhiteList()
        {
            using (var client = new WebClient())
            {
                try
                {
                    client.Proxy = null;
                    string data = client.DownloadString("https://raw.githubusercontent.com/noponet/noponet/main/whitelist.txt");
                    string[] lines = data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    this.whiteList = lines;
                    //Console.WriteLine(lines.Length + " Whitelisted sites loaded");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred loading keywords: " + ex.Message);
                }
            }
            this.localWhiteList = this.LoadArrayFromFile("C:\\Program Files (x86)\\noponet\\lists\\whitelist.txt");
        }

        public void refresh()
        {
            loadWhiteList();
            loadKeywords();

        }
        public void downloadTrollPage()
        {
            try
            {
                string url = "https://raw.githubusercontent.com/nopolifelock/noponet/main/trollpage/troll.html";
                var client = new WebClient();
                client.Proxy = null;
                this.trollpage = client.DownloadString(url);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }
        public void loadKeywords()
        {
            using (var client = new WebClient())
            {
                try
                {
                    client.Proxy = null;
                    string data = client.DownloadString("https://raw.githubusercontent.com/noponet/noponet/main/keywords.txt");
                    string[] lines = data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Equals(""))
                            lines[i] = "wiki";
                    }
                    this.keywords = lines;
                    //Console.WriteLine(lines.Length + " Keywords loaded");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred loading keywords: " + ex.Message);
                }
            }
            this.localKeywords = this.LoadArrayFromFile("C:\\Program Files (x86)\\noponet\\lists\\keywords.txt");
        }

        private Boolean isSafe(string url)
        {
            for (int i = 0; i < whiteList.Length; i++)
            {
                if (whiteList[i].Equals(url))
                {
                    return true;
                }
                else
                {

                }

            }
            for (int j = 0; j < keywords.Length; j++)
            {
                if (url.Contains(keywords[j]) && !keywords[j].Equals(""))
                {
                    return true;
                }
            }

            if (localWhiteList != null)
                for (int i = 0; i < localWhiteList.Length; i++)
                {
                    if (localWhiteList[i].Equals(url))
                    {
                        return true;
                    }
                    else
                    {

                    }

                }
            if (localKeywords != null)
                for (int j = 0; j < localKeywords.Length; j++)
                {
                    if (url.Contains(localKeywords[j]) && !localKeywords[j].Equals(""))
                    {
                        return true;
                    }
                }

            return false;

        }

        private List<ProxyConnection> connections = new List<ProxyConnection>();
        public void run()
        {
            new Thread(this.feedServer.run).Start();
            do
            {

                this.loadKeywords();
                this.loadWhiteList();
                Console.WriteLine("whitelist loaded");
                Thread.Sleep(3000);
            } while ((this.keywords.Length == 1) || (this.whiteList.Length == 1));


            //this.loadWhiteList();

            IPAddress ipAddress = IPAddress.Parse("0.0.0.0");
            int port = 8000;

            TcpListener listener = new TcpListener(ipAddress, port);
            listener.Start();

            Console.WriteLine("Listening for connections on port {0}...", port);
            Timer timer = new Timer(checkConnections, null, 0, 15000);
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                //Console.WriteLine("Accepted connection from {0}", client.Client.RemoteEndPoint);
                //new Thread(new ProxyConnection(client).parentRun).Start();
                ProxyConnection con = new ProxyConnection(client, this);
                con.start();
                connections.Add(con);
                // Add code here to handle the incoming connection...


            }


        }
        private void checkConnections(Object stateinfo)
        {
            int count = 0;
            for (int i = 0; i < connections.Count; i++)
            {
                ProxyConnection conn = connections[i];
                if ((currentTimeMillis() - conn.getLastActive()) > 10000)
                {
                    count++;
                    conn.close();
                    connections.Remove(conn);

                }

            }
            //Console.WriteLine(count + " threads closed");
            //Console.WriteLine(Process.GetCurrentProcess().Threads.Count);
        }
        public static long currentTimeMillis()
        {
            long currentTimeMillis = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            return currentTimeMillis;
        }

        class ProxyConnection
        {
            private NetworkStream stream;
            private StreamReader reader;
            private StreamWriter writer;
            private Thread thread;
            private Boolean shouldStop = false;
            private ProxyServer server;
            private long lastActive = currentTimeMillis();
            public ProxyConnection(TcpClient client, ProxyServer server)
            {
                this.server = server;
                client.Client.SendTimeout = 5000;
                //client.Client.ReceiveTimeout = 5000;
                this.stream = client.GetStream();
                this.reader = new StreamReader(this.stream);
                this.writer = new StreamWriter(this.stream);


            }

            public void start()
            {
                this.thread = new Thread(this.run);
                this.thread.Start();
            }
            public long getLastActive()
            {
                return lastActive;
            }
            public void run()
            {



                try
                {
                    string request = reader.ReadLine();

                    //String host = request.Substring(8, request.IndexOf(":") - 8);
                    String header;
                    do
                    {
                        header = reader.ReadLine();
                        if (header == null)
                            return;
                        //Console.WriteLine(header);
                    } while (header.Length > 1);

                    Match match = Regex.Match(request, @"CONNECT\s([^\:]+)\:(\d+)\sHTTP/(\d\.\d)");
                    string host = match.Groups[1].Value;
                    string poop = match.Groups[3].Value;

                    int port = int.Parse(match.Groups[2].Value);
                    Console.WriteLine(host);
                    if (!server.isSafe(host))
                    {
                        
                        this.server.feedServer.send(host);
                        stream.Close();
                        return;
                    }

                        try
                        {

                            writer.Write("HTTP/" + match.Groups[3].Value + " 200 Connection established\r\n");
                            writer.Write("Proxy-agent: Simple/0.1\r\n");
                            writer.Write("\r\n");
                            writer.Flush();

                        /*
                        Socket forwardSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        forwardSocket.ReceiveTimeout = 5000;
                        //forwardSocket.SendTimeout = 5000;

                        IPAddress ipAddress = Dns.GetHostAddresses(host)[0];
                        IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
                        forwardSocket.Connect(endPoint);
                        NetworkStream forwardStream = new NetworkStream(forwardSocket);
                        //forwardSocket.
                        */

                        TcpClient forwardClient = new TcpClient(host, port);
                        NetworkStream forwardStream = forwardClient.GetStream();

                        if (true)
                            {
                                try
                                {
                                    if (!shouldStop)
                                    {

                                        Thread remoteToClient = new Thread(() => ForwardData(forwardStream, this.stream));
                                        remoteToClient.Start();


                                        ForwardData(this.stream, forwardStream);
                                    }
                                }
                                catch (System.InvalidOperationException e)
                                {
                                    return;
                                }
                            }
                            else
                            {
                                return;
                            }

                        }
                        catch (Exception e)
                        {
                            return;
                        }

                }
                catch (Exception e)
                {
                    return;
                }
            }


            private void ForwardData(NetworkStream inputStream, NetworkStream outputStream)
            {
                try
                {
                    try
                    {
                        try
                        {
                            byte[] buffer = new byte[4096];
                            int read = 0; ;
                            do
                            {

                                if (!shouldStop)
                                {
                                    read = inputStream.Read(buffer, 0, buffer.Length);
                                    if (read > 0)
                                    {
                                        outputStream.Write(buffer, 0, read);
                                        //Console.Write(Encoding.UTF8.GetString(buffer));
                                        this.lastActive = currentTimeMillis();
                                        if (inputStream.DataAvailable == false)
                                        {
                                            outputStream.Flush();
                                        }
                                    }
                                }
                            } while (read > 0);
                        }
                        finally
                        {
                            if (outputStream.CanWrite)
                            {
                                outputStream.Close();
                            }
                        }
                    }
                    finally
                    {
                        if (inputStream.CanRead)
                        {
                            inputStream.Close();

                        }
                    }
                }
                catch (Exception)
                {
                    // Handle the exception
                    return;
                }
            }




            public void close()
            {
                shouldStop = true;
                if (this.stream.CanRead || this.stream.CanWrite)
                {
                    this.reader.Close();
                    this.writer.Close();
                    this.stream.Close();
                }
                this.thread.Join();
                this.thread.Abort();


                //this.server.connections.Remove(this);
            }

        }

    }
}
