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
    class Program
    {
        static void Main(string[] args)
        {
            // my test requests are sended string by string
            string[] test = { "GET ", "hahsd ", "HTTP/1.1\r\n", "adfad:\r\nasdf adfa:\r\n\r\n" };

            // put strings into my testnetworstream class
            WebServer.ns = new TestNetworkStream(test);

            // set response template
            const string DefaultTemplate = "HTTP/1.1 200 OK\r\n" + "Content-Type: text/html; charset=utf-8\r\n" +
                "\r\n\r\n" + "<html>ID Number: {0}<br>" + "DateTime.Now: {1}<br>" + "Requested URL: {2}</html>";

            // start webserver
            WebServer.Start(4220, DefaultTemplate);

            
            
            
            
        }
        
    }

    

    
    
}
