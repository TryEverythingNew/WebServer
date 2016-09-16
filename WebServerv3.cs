//Name: xueliang sun   ID:11387859
using System;
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
    public class WebServer
    {
        public static TestNetworkStream ns;
        //public static NetworkStream ns;



        // start the wevserver with port and response template
        public static bool Start(int port, string responseTemplate)
        {

            // create a server and a client
            TcpListener server = new TcpListener(IPAddress.Any, port);
            TcpClient client = null;

            server.Start();


            // link the client to the server
            try
            {
                client = server.AcceptTcpClient();
            }
            catch (SocketException e)
            {
                return false;
            }

            // buffer stores bytes from network stream, URI stores the URI string
            byte[] buffer = new byte[1024];
            StringBuilder URI = new StringBuilder();

            NetworkStream ns = client.GetStream();

            bool read_conti = true; // whether to continue read from buffer
            int i = 0, j = 0;  // i: current reader position of buffer, j: current position of a comparison string (a string to compare with the buffer content)
            int read = 0;   // number of read back bytes from newwork stream
            //int sp = 0; //  reader's start position for current comparison 
            string[] corr = { "GET ", "URI", "HTTP/1.1\r\n" };  // comparison string, used to compare
            string tmp = null;
            int comp = 0, colon = 0, rn = 0; // count the number of string comparison that has been made
            // colon: number of colon appeared in current line, rn: number of \r\n continuously appeared just before current reader position
            bool prev_is_r = false; // whether previous byte is \r

            //string request = null;  // for debug only;

            while (read_conti)  // "while" is the loop for reading from network stream
            {

                try
                {
                    i = 0;
                    read = ns.Read(buffer, 0, buffer.Length);
                    // request = Encoding.ASCII.GetString(buffer, 0, read);
                }
                catch (Exception e)
                {
                    Quit(ns, client);
                    return false;
                }
                if (read == 0)  // quit and return if no bytes read back at all
                {
                    Quit(ns, client);
                    return false;
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
                                return false;
                            }
                        }
                    }

                    if (comp == 1)  // when comparing with 2nd comparison string, we need to care about space " " and \r\n
                    {   // this part is validation of URI
                        for (; i < read; i++)
                        {
                            tmp = Encoding.ASCII.GetString(buffer, i, 1);
                            if (tmp == " ")
                            {
                                if (j == 0)
                                {
                                    return false; // space is not allowed in first byte of URI
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
                            continue;  // continue while loop
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
                            if (rn > 0)
                            {
                                Quit(ns, client);
                                return false;   // the colon or space for header field appears at the first char of a line
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
                            if (rn == 2)    // we meet a double \r\n, and this request is valid
                            {
                                read_conti = false;
                            }
                            else if (colon == 0)    // when rn == 1, we expect colon number more than 0
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

            // set the response
            string response = String.Format(responseTemplate, "11387859", DateTime.Now.ToString(), URI);

            // write the response and quit
            byte[] buffer_response = Encoding.UTF8.GetBytes(response);
            ns.Write(buffer_response, 0, buffer_response.Length);
            Quit(ns, client);
            return true;
        }

        // quit is to dispose the network stream and client
        public static void Quit(Stream ns, TcpClient client)
        {
            ns.Dispose();
            client.Close();
        }
    }

    // testnetworkstream is a new class to design some bytes for network stream to read; it is for tests
    public class TestNetworkStream : Stream
    {
        //NetworkStream ns;
        string[] str;
        byte[] buf; // member var to interact with read function and main program
        int pos;

        public TestNetworkStream(string[] s)
        {
            pos = 0;
            str = s;
            buf = Encoding.ASCII.GetBytes(str[pos]);
        }

        // override the read function to manually read bytes from member var buf to buffer
        override public int Read(byte[] buffer, int offset, int count)
        {
            if (pos >= str.Length)
            {
                return 0;
            }

            buf = Encoding.ASCII.GetBytes(str[pos]);
            pos++;



            if (offset + count > buffer.Length)
            {
                return 0; // since incorrect parameter;
            }

            for (int i = 0; i < count; i++)
            {
                if (buf == null)
                    return 0;
                else if (i >= buf.Length)
                {
                    return i;
                }
                else
                {
                    buffer[i + offset] = buf[i];
                }

            }
            return count;
        }

        // get bytes manually
        public void SetBuf(byte[] buffer)
        {
            buf = buffer;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool CanSeek
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool CanWrite
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }



        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }




    }
}
