using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CS422
{
    class Program
    {
        static void Main(string[] args)
        {
            const string DefaultTemplate = "HTTP/1.1 200 OK\r\n" + "Content-Type: text/html\r\n" +
                "\r\n\r\n" + "<html>ID Number: {0}<br>" + "DateTime.Now: {1}<br>" + "Requested URL: {2}</html>";
            WebServer.Start(4220, DefaultTemplate);
        }
    }

    public class WebServer
    {
        public static bool Start(int port, string responseTemplate)
        {
            IPAddress[] ipAddress = Dns.GetHostAddresses("192.168.1.101");
            TcpListener server = new TcpListener(ipAddress[0], port);

            //TcpListener server = new TcpListener(IPAddress.Any, port);
            TcpClient client = null;

            server.Start();



            try {
                client = server.AcceptTcpClient();
            }
            catch (SocketException e)
            {
                return false;
            }


            byte[] buffer = new byte[1024];
            StringBuilder URI = new StringBuilder();


            NetworkStream ns = client.GetStream();
            // for test
            //TestNetworkStream ns = (TestNetworkStream) client.GetStream();

            bool read_conti = true;
            int i = 0, j = 0;  // current reader position
            int read = 0;
            //int sp = 0; //  reader's start position for current comparison 
            string[] corr = { "GET ", "URI", "HTTP/1.1\r\n" };
            string tmp = null;
            int comp = 0, colon = 0, rn = 0; // count the number of string comparison that has been made
            bool prev_is_r = false;

            string request = null;

            while (read_conti)
            {

                try
                {
                    i = 0;
                    read = ns.Read(buffer, 0, buffer.Length);
                    request = Encoding.ASCII.GetString(buffer, 0, read);
                }
                catch (Exception e)
                {
                    Quit(ns, client);
                    return false;
                }
                if (read == 0)
                {
                    Quit(ns, client);
                    return false;
                }

                while (comp <= 2 && i < read)
                {
                    if (comp == 0 || comp == 2)
                    {
                        for (; i < read; i++)
                        {
                            tmp = Encoding.ASCII.GetString(buffer, i, 1);
                            if (tmp == corr[comp][j].ToString())
                            {
                                j++;
                                if (j >= corr[comp].Length)
                                {
                                    comp++;
                                    if (comp == 3)
                                    {
                                        rn = 1;
                                    }
                                    j = 0;
                                    i++;
                                    break;
                                }
                            }
                            else
                            {
                                Quit(ns, client);
                                return false;
                            }
                        }
                    }

                    if (comp == 1)
                    {
                        for (; i < read; i++)
                        {
                            tmp = Encoding.ASCII.GetString(buffer, i, 1);
                            if (tmp == " ")
                            {
                                prev_is_r = false;
                                comp++;
                                j = 0;
                                i++;
                                break;
                            }
                            else
                            {
                                URI.Append(tmp);

                                if (tmp == "\r")
                                {
                                    prev_is_r = true;
                                }
                                else if (tmp == "\n" && prev_is_r)
                                {
                                    Quit(ns, client);
                                    return false;
                                }
                                else
                                {
                                    prev_is_r = false;
                                }
                            }
                        }
                        if (tmp != " ")
                        {
                            continue;
                        }
                    }
                }



                if (comp > 2 && i < read)
                {
                    for (; i < read; i++)
                    {
                        tmp = Encoding.ASCII.GetString(buffer, i, 1);
                        if (tmp == ":")
                        {
                            if (rn > 0)
                            {
                                Quit(ns, client);
                                return false;   // the colon for header field appears at the first char of a line
                            }
                            colon++;
                            prev_is_r = false;
                            rn = 0;
                        }
                        else if (tmp == "\r")
                        {
                            prev_is_r = true;
                        }
                        else if (tmp == "\n" && prev_is_r)
                        {
                            rn++;
                            prev_is_r = false;
                            if (rn == 2)
                            {
                                read_conti = false;
                            }
                            else if (colon == 0)
                            {
                                Quit(ns, client);
                                return false; // the colon doesn't appear at this line at all
                            }
                            colon = 0;
                        }
                        else
                        {
                            prev_is_r = false;
                            rn = 0;
                        }
                    }

                }
            }

            // read_conti == false;
            string response = String.Format(responseTemplate, "11387859", DateTime.Now.ToString(), request);
            byte[] buffer_response = Encoding.ASCII.GetBytes(response);
            ns.Write(buffer_response,0,buffer_response.Length);
            Quit(ns, client);
            return true;
        }

        public static void Quit(NetworkStream ns, TcpClient client)
        {
            ns.Dispose();
            client.Close();
        }
    }

    
}
