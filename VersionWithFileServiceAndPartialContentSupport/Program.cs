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
            // always give an absolute address for file root
            // in linux you want to declare file root like below, actually this also works in windows
            //StandardFileSystem fs = StandardFileSystem.Create("/home/xsun/Downloads" + Path.DirectorySeparatorChar + "test");
            // in windows, you want (not working in linux)
            StandardFileSystem fs = StandardFileSystem.Create("D:" + Path.DirectorySeparatorChar + "debug");
            FilesWebService fileservice = new FilesWebService(fs);
            WebServer.AddService(fileservice);
            //WebServer.AddService(ds); // no need to add demoservice, otherwise, it starts with '/' and conflicts with file service


            Thread t1 = new Thread(new ThreadStart(Server)); // a thread for run the server
            t1.Start();
            Thread.Sleep(100000); // after 30 seconds, stop the server
            //WebServer.Stop();
        }

        static void Server()
        {
            WebServer.Start(4022, 16); // setup the server, start it
        }
    }
}
