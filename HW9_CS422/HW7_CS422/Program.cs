using System;
using System.Collections.Generic;
using System.IO;
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
            StandardFileSystem fs = StandardFileSystem.Create("D:" + Path.DirectorySeparatorChar + "test");
            FilesWebService fileservice = new FilesWebService(fs);
            WebServer.AddService(fileservice);
            //WebServer.AddService(ds); // no need to add demoservice, otherwise, it starts with '/' and conflicts with file service


            Thread t1 = new Thread(new ThreadStart(Server)); // a thread for run the server
            t1.Start();
            Thread.Sleep(100000); // after 30 seconds, stop the server
            WebServer.Stop();
        }

        static void Server()
        {
            WebServer.Start(4022, 16); // setup the server, start it
        }
    }
}
