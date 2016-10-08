using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CS422
{
    class Program
    {
        static void Main(string[] args)
        {
            DemoService ds = new DemoService();

            WebServer.AddService(ds); // add service

            Thread t1 = new Thread(new ThreadStart(Server)); // a thread for run the server
            t1.Start();
            Thread.Sleep(30000); // after 30 seconds, stop the server
            WebServer.Stop();
        }

        static void Server()
        {
            WebServer.Start(4022, 16); // setup the server, start it
        }
    }
}
