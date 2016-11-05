using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CS422
{
    public class MyTask // MyTask class with function of processing TCP socket connection
    {
        public TcpClient client;

        public void Go()
        {
            // deal with client
            WebRequest wr = WebServer.GetRequest(client);
            //if(wr == null)
            //{
            //    client.Close(); // client with an invalid request should already be closed
            //}
            if (wr != null) // if request is not null, try to find service
            {
                bool findservice = false;
                for (int i = 0; i < WebServer.webservice.Count; i++) // traverse list to find service
                {
                    string tmp = WebServer.webservice[i].ServiceURI;
                    if (wr.URI.StartsWith(tmp))
                    {
                        WebServer.webservice[i].Handler(wr); // if service found, then call its handler
                        findservice = true;
                    }
                }
                if (!findservice) // no service found, call not found service
                {
                    wr.WriteNotFoundResponse("<html>Service Not Found.</html>");
                }
                client.Close();
            }
        }
    }

    public class WebServer
    {
        static int timeoutperread = 1000;
        static int timeoutperclient = 10000;
        public static NetworkStream ns;
        static BlockingCollection<MyTask> coll;  //record the collection of tasks 
        static Thread[] threads = null;   // store threads
        static int num_availthread; // how many threads are available
        public static List<WebService> webservice = new List<WebService>(); // list to store webservice
        static Thread thread_listen = null; // thread for accepting new client
        static bool running = true;
        static TcpListener server;

        public static void AddService(WebService service) // for adding webservice
        {
            lock (webservice)
            {
                if (service != null)
                {
                    webservice.Add(service);
                }
            }
        }

        public static void Stop() // to stop the server
        {
            if (threads != null && coll != null)// first, we add lots of null tasks into collection, because null task ends thread
            {
                for (int i = 0; i < threads.Length; i++)
                {
                    MyTask tmp = null;
                    coll.Add(tmp);
                }
                for (int i = 0; i < threads.Length; i++) // wait for all threads to join
                {
                    if (threads[i].IsAlive)
                    {
                        threads[i].Join();  
                    }
                }
                running = false;
                server.Stop();
                thread_listen.Abort();   // use server.stop to end accepting new client and also terminate that thread
                coll = null;
            }

        }

        public static void ThreadWorkFunc() // function delegate for threads
        {
            while (coll != null)
            {
                MyTask tmp = coll.Take();   // take a task, and do tasks by function Go
                if (tmp != null)
                {
                    num_availthread--;
                    tmp.Go();
                    num_availthread++;
                }
                else
                {
                    break;
                }
            }
        }

        // start the wevserver with port and response template
        public static bool Start(int port, int num_thread)
        {
            // initialize server and threads
            string host = Dns.GetHostName();
            IPHostEntry heserver = Dns.GetHostEntry(host);
            foreach (IPAddress curAdd in heserver.AddressList)
            {


                // Display the type of address family supported by the server. If the
                // server is IPv6-enabled this value is: InternNetworkV6. If the server
                // is also IPv4-enabled there will be an additional value of InterNetwork.
                Console.WriteLine("AddressFamily: " + curAdd.AddressFamily.ToString());

                // Display the ScopeId property in case of IPV6 addresses.
                if (curAdd.AddressFamily.ToString() == ProtocolFamily.InterNetworkV6.ToString())
                    Console.WriteLine("Scope Id: " + curAdd.ScopeId.ToString());


                // Display the server IP address in the standard format. In 
                // IPv4 the format will be dotted-quad notation, in IPv6 it will be
                // in in colon-hexadecimal notation.
                Console.WriteLine("Address: " + curAdd.ToString());

                // Display the server IP address in byte format.
                Console.Write("AddressBytes: ");



                Byte[] bytes = curAdd.GetAddressBytes();
                for (int i = 0; i < bytes.Length; i++)
                {
                    Console.Write(bytes[i]);
                }

                Console.WriteLine("\r\n");

            }

            //server = new TcpListener(IPAddress.Any, port);
            server = new TcpListener(IPAddress.IPv6Any, port);
            server.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

            server.Start();
            thread_listen = new Thread(ThreadWorkFunctionListen);
            thread_listen.Start();
            if (num_thread <= 0)
            {
                num_thread = 64;
            }
            num_availthread = num_thread;

            //initialize collections and many other variables
            coll = new BlockingCollection<MyTask>(num_thread);
            threads = new Thread[num_thread];
            for (int i_thread = 0; i_thread < num_thread; i_thread++)
            {
                threads[i_thread] = new Thread(ThreadWorkFunc);
                threads[i_thread].Start();
                while (!threads[i_thread].IsAlive)
                {
                    // use while loop to make sure every thread is alive
                }
            }

            

            return true;
        }

        public static void ThreadWorkFunctionListen() // give a special thread for listening thread, which is for accepting clients
        {
            while (running)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient(); // accept new clients, and put them into collection of tasks
                    MyTask task_tmp = new MyTask();
                    task_tmp.client = client;
                    coll.Add(task_tmp);
                }
                catch (SocketException e)
                {
                    //throw new Exception("unable to get client"); //just a warning that you don't get a client, most times it's due to listener has stopped
                }
            }
        }

        // quit is to dispose the network stream and client
        public static void Quit(Stream ns, TcpClient client)
        {
            ns.Dispose();
            client.Close();
        }

        public static WebRequest GetRequest(TcpClient client) // call private function to return webrequest
        {
            return BuildRequest(client);
        }

        // core function to return a web request, a lot of string parsing are done here
        private static WebRequest BuildRequest(TcpClient client)
        {
            if (client == null) return null;

            string method = null;
            string version = null;
            Dictionary<string, string> headers = new Dictionary<string, string>();
            ConcatStream body;
            MemoryStream ms = new MemoryStream();

            ///this is the function to check whether request is valid or not
            // buffer stores bytes from network stream, URI stores the URI string
            byte[] buffer = new byte[1024];
            StringBuilder URI = new StringBuilder();
            StringBuilder header = new StringBuilder();
            StringBuilder header_value = new StringBuilder();

            NetworkStream ns = client.GetStream();
            ns.ReadTimeout = timeoutperread; // set the read timeout
            DateTime localDate = DateTime.Now; // setup the current time
            bool notexceedtimeout = true;//(DateTime.Now - localDate).Milliseconds;
            double read_size = 0; // this is the size for reading from network stream
            int threshold_firstline = 2048; // this is the size threshold for reading up to the first line end
            int threshold_uptobody = 100 * 1024; // this is the size threshold for reading up to the body content

            bool read_conti = true; // whether to continue read from buffer
            int i = 0, j = 0;  // i: current reader position of buffer, j: current position of a comparison string (a string to compare with the buffer content)
            int read = 0;   // number of read back bytes from newwork stream
            //int sp = 0; //  reader's start position for current comparison 
            string[] corr = { "GET ", "URI", "HTTP/1.1\r\n" };  // comparison string, used to compare
            string tmp = null;
            int comp = 0, colon = 0, rn = 0; // count the number of string comparison that has been made
            // colon: number of colon appeared in current line, rn: number of \r\n continuously appeared just before current reader position
            bool prev_is_r = false; // whether previous byte is \r
            bool is_before_colon = true; // whether we are before a colon in current line;

            //string request = null;  // for debug only;

            while (read_conti && (notexceedtimeout = ((DateTime.Now - localDate).Milliseconds < timeoutperclient)))  // "while" is the loop for reading from network stream
            {

                try
                {
                    read_size += read;
                    if(read_size > threshold_uptobody || (read_size > threshold_firstline && comp == 0)) // read up too many bytes, need to stop the client the networkstream
                    {
                        Quit(ns, client);
                        return null;
                    }
                    i = 0;
                    read = ns.Read(buffer, 0, buffer.Length);
                    string debug = Encoding.ASCII.GetString(buffer, 0, read);
                    // request = Encoding.ASCII.GetString(buffer, 0, read);
                }
                catch (Exception e)
                {
                    Quit(ns, client);
                    return null;
                }
                if (read == 0)  // quit and return if no bytes read back at all
                {
                    Quit(ns, client);
                    return null;
                }

                while (comp <= 2 && i < read)   // if we are comparing to the first 3 comparison strings and not upto buffer end
                {
                    if (comp == 0 || comp == 2)
                    {
                        for (; i < read; i++)   // compare to comparison string byte by byte
                        {
                            tmp = Encoding.ASCII.GetString(buffer, i, 1);
                            if (tmp == corr[comp][j].ToString())
                            {
                                j++;
                                if (j >= corr[comp].Length) // comparison of current string is finished
                                {
                                    comp++;
                                    if (comp == 3)
                                    {
                                        rn = 1; // increment rn because the 3rd comparison string ends with \r\n
                                    }
                                    j = 0;
                                    i++;
                                    break;
                                }
                            }
                            else    // different from a comparison string
                            {
                                Quit(ns, client);
                                return null;
                            }
                        }
                    }

                    if (comp == 1)  // when comparing with 1st comparison string, we need to care about space " " and \r\n
                    {   // this part is validation of URI
                        for (; i < read; i++)
                        {
                            tmp = Encoding.ASCII.GetString(buffer, i, 1);
                            if (tmp == " ")
                            {
                                if (j == 0)
                                {
                                    Quit(ns, client);
                                    return null; // space is not allowed in first byte of URI
                                }
                                prev_is_r = false;
                                comp++;
                                j = 0;
                                i++;
                                break;
                            }
                            else
                            {
                                URI.Append(tmp); // append this string to our URI string
                                j++;

                                if (tmp == "\r")
                                {
                                    prev_is_r = true;
                                }
                                else if (tmp == "\n" && prev_is_r)  // \r\n should not appear here
                                {
                                    Quit(ns, client);
                                    return null;
                                }
                                else
                                {
                                    prev_is_r = false;
                                }
                            }
                        }
                    }
                }



                if (comp > 2 && i < read)   // we are in header field, and need to care about colon ":" and \r\n
                {
                    for (; i < read; i++)
                    {
                        tmp = Encoding.ASCII.GetString(buffer, i, 1);
                        if (tmp == ":" || tmp == " ")
                        {
                            if (rn > 0 || (tmp == " " && is_before_colon))
                            {
                                Quit(ns, client);
                                return null;   // the colon or space for header field appears at the first char of a line
                            }
                            if (tmp == ":")
                            {
                                is_before_colon = false;
                                colon++;
                            }
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
                            if (header.ToString() != null)
                            {
                                headers.Add(header.ToString(), header_value.ToString());
                                header = new StringBuilder(); // clear the header string in order to support new header.
                                header_value = new StringBuilder();// clear the header value string in order to support new header's value.
                            }
                            is_before_colon = true;
                            prev_is_r = false;
                            if (rn == 2)    // we meet a double \r\n, and this request is valid
                            {
                                ms.Write(buffer, i + 1, read - (i + 1));
                                read_conti = false;
                            }
                            else if (colon == 0)    // when rn == 1, we expect colon number more than 0
                            {
                                Quit(ns, client);
                                return null; // the colon doesn't appear at this line at all
                            }
                            colon = 0;
                        }
                        else
                        {
                            if (is_before_colon)
                            {
                                if (prev_is_r)
                                {
                                    header.Append("\r");
                                }
                                header.Append(tmp);
                            }
                            else
                            {
                                if (prev_is_r)
                                {
                                    header_value.Append("\r");
                                }
                                header_value.Append(tmp);
                            }
                            prev_is_r = false;
                            rn = 0;
                        }
                    }

                }
            }
            if (!notexceedtimeout) // if exceeds timeout limit, then we need to return null
            {
                Quit(ns, client);
                return null;
            }
            method = string.Copy(corr[0]); // simple copy to get method and version
            version = string.Copy(corr[2]);

            bool flag = false; // try to get content length header
            string content_length = null;
            foreach (KeyValuePair<string, string> kvp in headers)
            {
                if (kvp.Key.ToLower() == "Content-Length".ToLower())
                {
                    
                    content_length = kvp.Value;
                    break;
                }
            }
            string test = Encoding.ASCII.GetString(buffer, 0, read); // just for testing purpose

            int body_length = -1; // try to get body length
            if (content_length != null && int.TryParse(content_length, out body_length))
            {
                body_length = int.Parse(content_length);
            }
            else
            {
                body_length = -1;
            }
            //body_length = 100; // for debug only
            if (body_length >= 0) // concatenate to a stream
            {
                flag = true; // whether body length is shown
                body = new ConcatStream(ms, ns, body_length);
            }
            else
            {
                body = new ConcatStream(ms, ns);
            }
            // build the request
            WebRequest wr = new WebRequest(method, URI.ToString(), version, headers, body, ns, flag);
            return wr;
        }

    }

    public class WebRequest
    {
        public string method, URI, version;
        public Dictionary<string, string> header;
        Stream body;
        private NetworkStream ns;
        bool flag; // indicate whether we have content-length header
        byte[] buffer = new byte[1000];
        // utf8_enabled means whether we uses utf-8 encoding, which is useful for supporting many characters
        public static bool utf8_enabled = true; // this is to indicate whether we use UTF-8 encoding for content

        // construction and initialize the member variables
        public WebRequest(string m, string U, string v, Dictionary<string, string> h, Stream b, NetworkStream n, bool f)
        {
            method = m;
            URI = U;
            version = v;
            header = h;
            body = b;
            ns = n;
            flag = f;
        }

        public long BodyLength
        {
            get
            {
                //int read = -1;
                //while( read != 0)
                //{
                //    read = ns.Read(buffer, 0, buffer.Length);
                //    (body as ConcatStream).s1.Write(buffer, 0 , read);
                //} //  this is used for returning the actual request body length.
                if (flag)
                {
                    return body.Length;
                }

                throw new NotSupportedException();
            }
        }

        public void WriteNotFoundResponse(string pageHTML) // service function to write response of not found response
        {
            const string Template = "HTTP/1.1 404 Not Found\r\n" + "Content-Type: text/html\r\n" + "Content-Length: {0}\r\n\r\n";
            string response = string.Format(Template, pageHTML.Length) + pageHTML;
            byte[] buffer_response = Encoding.ASCII.GetBytes(response);
            ns.Write(buffer_response, 0, buffer_response.Length);
            ns.Dispose();
        }
        public bool WriteHTMLResponse(string htmlString) // service function for writting desired service template
        {
            string Template = null;
            string response = null;
            if (utf8_enabled)
            {
                Template = "HTTP/1.1 200 OK\r\n" + "Content-Type: text/html; charset=UTF-8\r\n" + "Content-Length: {0}\r\n\r\n";
                response = string.Format(Template, Encoding.UTF8.GetBytes(htmlString).Length) + htmlString;
            }
            else
            {
                Template = "HTTP/1.1 200 OK\r\n" + "Content-Type: text/html\r\n" + "Content-Length: {0}\r\n\r\n";
                response = string.Format(Template, Encoding.ASCII.GetBytes(htmlString).Length) + htmlString;
            }
            byte[] buffer_response = null;
            // choose encoding
            if(utf8_enabled) buffer_response = Encoding.UTF8.GetBytes(response);
            else buffer_response = Encoding.ASCII.GetBytes(response);
            
            ns.Write(buffer_response, 0, buffer_response.Length);
            ns.Dispose();
            return true;
        }

        public NetworkStream NetStream
        {
            get
            {
                return ns;
            }
        }
        

    }

    public abstract class WebService // abstract class for webservice, which can be inherited to include all possible webservices
    {
        public abstract void Handler(WebRequest req);
        /// <summary>
        /// Gets the service URI. This is a string of the form:
        /// /MyServiceName.whatever
        /// If a request hits the server and the request target starts with this
        /// string then it will be routed to this service to handle.
        /// </summary>
        public abstract string ServiceURI
        {
            get;
        }
    }

    public class DemoService : WebService // a sample service just for demo
    {
        public override string ServiceURI // if URI starts from '/'
        {
            get
            {
                return "/";
            }
        }

        public override void Handler(WebRequest req) // to deal with the web request
        {
            const string c_template = "<html>This is the response to the request:<br>" + "Method: {0}<br>Request-Target/URI: {1}<br>" +
                                    "Request body size, in bytes: {2}<br><br>" + "Student ID: {3}</html>";
            string response = string.Format(c_template, req.method, req.URI, req.BodyLength, "11387859");
            req.WriteHTMLResponse(response);
        }

    }


}
