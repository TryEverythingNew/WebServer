using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS422
{
    // this class is used for checking path valid or not with static fields
    public class InvalidChar
    {
        // path seperators
        static char[] sep = { Path.DirectorySeparatorChar };//'\\', '/', 

        // check whether it is a valid path
        public static bool CheckValidChar(string path)
        {
            if (path == null || path.Length == 0) return false;
            string[] tmp = path.Split(sep);
            return (tmp.Length >= 1) ? true : false;
        }

        // get the last piece of string to be the file name or dir name
        public static string GetNameFromPath(string path)
        {
            if (path == null || path.Length == 0) return null;
            string[] s_tmp = path.Split(sep, System.StringSplitOptions.RemoveEmptyEntries);
            return (s_tmp.Length > 0) ? s_tmp[s_tmp.Length - 1] : null;
        }

        // get the parent diretory's path from current path
        public static string GetParentFromPath(string path)
        {
            if (path == null || path.Length == 0) return null;
            string[] s_tmp = path.Split(sep, System.StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder();
            for(int i = 0; i < s_tmp.Length-1; i++)
            {
                sb.Append(s_tmp[i]);
            }
            return sb.ToString();
        }
    }

    public abstract class Dir422
    {
        public abstract string Name { get; } // directory name
        public abstract IList<Dir422> GetDirs(); // Interface List IList gives more flexibility, not required to be a list
        public abstract IList<File422> GetFiles(); // IEnurable interface gives enurable flexibility, here we want a list
        public abstract Dir422 Parent { get; } // parent directory
        public abstract bool ContainsFile(string fileName, bool recursive); // check whether contain file
        public abstract bool ContainsDir(string dirName, bool recursive); // check whether contain dir
        public abstract File422 GetFile(string fileName); // get the file object
        public abstract Dir422 GetDir(string dirName); // get the dir object
        public abstract Dir422 CreateDir(string dirName); // if existed, return that directry, nothing change
        public abstract File422 CreateFile(string fileName); // if existed, clear the old one, return that file?
    }


    public abstract class File422
    {
        public abstract Dir422 Parent { get; }
        public abstract string Name { get; }
        public abstract Stream OpenReadOnly(); // must set CanWrite Property to be false
        public abstract Stream OpenReadWrite();// open file with stream readable and writable
    }

    public abstract class FileSys422
    {
        public abstract Dir422 GetRoot(); // root to restrict access to files / dirs outside our system

        public virtual bool Contains(File422 file)
        {
            return Contains(file.Parent); // parent is Dir422 class
        }
        public virtual bool Contains(Dir422 dir)
        {
            if( object.ReferenceEquals(dir,null)) // == might be overloaded, so we need to use object reference equal
            {
                return false;
            }
            while(dir.Parent != null)
            {
                dir = dir.Parent;
            }
            return object.ReferenceEquals(dir, GetRoot()); // root's parent is null
        }
    }
    

    public class StdFSDir: Dir422
    {
        private string m_path; // path is necessary for operating system to track the true path / location
        StdFSDir parent;

        //constructor
        public StdFSDir(string path, StdFSDir parent_dir)
        {
            if (!Directory.Exists(path))
            {
                throw new ArgumentException("invalid path");
            }
            parent = parent_dir;
            m_path = path;
        }

        public override string Name
        {
            get
            {
                return InvalidChar.GetNameFromPath( string.Copy(m_path));
            }
        }

        public override Dir422 Parent
        {
            get
            {
                return parent;
            }
        }

        // check whether contain dir
        public override bool ContainsDir(string dirName, bool recursive)
        {
            //check valid path or not
            if (!InvalidChar.CheckValidChar(dirName))
                return false;
            // use dir_list to find the dir
            IList<Dir422> dir_list = GetDirs();
            if (dir_list == null) return false;
            foreach(Dir422 dir in dir_list)
            {
                if (dir.Name == dirName)
                    return true;
                if (recursive && dir.ContainsDir(dirName, recursive))
                    return true;
            }
            return false;
        }

        // check whether contain file
        public override bool ContainsFile(string fileName, bool recursive)
        {
            //check valid path or not
            if (!InvalidChar.CheckValidChar(fileName))
                return false;
            // use file_list to find the file
            IList<File422> file_list = GetFiles();
            if (file_list != null)
            {
                foreach (File422 file in file_list)
                {
                    if (file.Name == fileName)
                        return true;
                }
            }

            // if recurisive searching is true, traverse the sub dir
            if (recursive) {
                IList<Dir422> dir_list = GetDirs();
                if (dir_list == null) return false;
                foreach (Dir422 dir in dir_list)
                {
                    if (dir.ContainsFile(fileName, recursive))
                        return true;
                } 
            }
            return false;
        }

        public override Dir422 CreateDir(string dirName)
        {
            // check vaild path first
            if (!InvalidChar.CheckValidChar(dirName))
                return null;
            else
            {
                // create a new directory
                string path = m_path + Path.DirectorySeparatorChar + dirName;
                try
                {
                    DirectoryInfo dirinfo = Directory.CreateDirectory(path);
                    return new StdFSDir(dirinfo.FullName, this);
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine("Create directory failure" + e.Message);
                    return null;
                }
            }
        }

        public override File422 CreateFile(string fileName)
        {
            // check vaild path first
            if (!InvalidChar.CheckValidChar(fileName))
                return null;
            else
            {
                string path = m_path + Path.DirectorySeparatorChar + fileName;
                try
                {
                    // create the new file
                    FileStream fs = File.Create(path);
                    fs.Dispose();
                    return new StdFSFile(path, this);
                }catch(Exception e)
                {
                    Console.Out.WriteLine("Create file failure" + e.Message);
                    return null;
                }
            }
        }

        public override Dir422 GetDir(string dirName)
        {
            // return the dir object if exist
            if (!InvalidChar.CheckValidChar(dirName))
                return null;
            string path = m_path + Path.DirectorySeparatorChar + dirName;
            if (Directory.Exists(path))
            {
                return new StdFSDir(path, this);
            }
            else
            {
                return null;
            }
        }

        public override IList<Dir422> GetDirs()
        {
            // call Directory method to return the dirs
            if (Directory.Exists(m_path))
            {
                List<Dir422> dir_list = new List<Dir422>();
                foreach (string dir_path in Directory.GetDirectories(m_path))
                {
                    dir_list.Add(new StdFSDir(dir_path, this));
                }
                return dir_list;
            }
            else
            {
                return null;
            }
        }

        public override File422 GetFile(string fileName)
        {
            // similar to above
            if (!InvalidChar.CheckValidChar(fileName))
                return null;
            else
            {
                string path = m_path + Path.DirectorySeparatorChar + fileName;
                if (File.Exists(path))
                {
                    return new StdFSFile(path, this);
                }
                else
                {
                    return null;
                }
            }
        }

        public override IList<File422> GetFiles()
        {
            // similar to above
            List<File422> files = new List<File422>();
            foreach (string file in Directory.GetFiles(m_path))
            {
                files.Add(new StdFSFile(file, this));
            }
            return files;
        }

    }

    public class StdFSFile : File422
    {
        private string m_path;
        StdFSDir parent;

        // constructor, need to check path
        public StdFSFile(string path, StdFSDir parent_dir)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException("Invalid path for file");
            }
            else
            {
                m_path = path;
                parent = parent_dir;
            }
        }

        public override string Name
        {
            get
            {
                return InvalidChar.GetNameFromPath(string.Copy(m_path));
            }
        }

        public override Dir422 Parent
        {
            get
            {
                return parent;
            }
        }

        public override Stream OpenReadOnly()
        {
            return new FileStream(m_path, FileMode.Open, FileAccess.Read); // for filestream, you can have multiple readonly filestreams reading the same file
            // operating system won't let you read a file while some other streams are writting it
            // operating system won't let you write a file while some other streams are reading it
            // all above means that we need to simulate this property to protect our file access
            // you can keep a string list for keeping track of all the openned files or dirs
            // or you can design your own streams to open the files
        }

        public override Stream OpenReadWrite()
        {
            return new FileStream(m_path, FileMode.Open, FileAccess.ReadWrite);
        }
    }

    public class StandardFileSystem: FileSys422
    {
        //private StdFSDir m_root = "/"; // wrong with this string, because you are creating new root dir every time you GetRoot
        private readonly Dir422 r_root;
        
        // you need a path to construct a filesystem
        // and you can only create the filesystem through Static Create method, because the constructor is private
        private StandardFileSystem(string path)
        {
            if (InvalidChar.CheckValidChar(path)){
                r_root = new StdFSDir(path, null);
            }
            else
            {
                r_root = null;
            }
        }

        // static method to initailize a filesystem
        public static StandardFileSystem Create(string rootDir)
        {
            if (Directory.Exists(rootDir))
            {
                Directory.SetCurrentDirectory(rootDir);
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(rootDir);
                }
                catch(Exception e)
                {
                    return null;
                }
            }
            return new StandardFileSystem(rootDir);
        }
        public override Dir422 GetRoot()
        {
            return r_root;
        }
    }

    // memory file system, just contains constructor and getroot method
    public class MemoryFileSystem : FileSys422
    {
        private readonly Dir422 r_root;

        public MemoryFileSystem()
        {
            r_root = new MemFSDir("root", null);
        }

        public override Dir422 GetRoot()
        {
            return r_root;
        }
    }

    public class MemFSDir: Dir422
    {
        MemFSDir parent;
        string name;
        Dictionary<string, MemFSFile> file_list = new Dictionary<string, MemFSFile>(); // hold all the files inside
        Dictionary<string, MemFSDir> dir_list = new Dictionary<string, MemFSDir>(); // hold all the sub dirs

        public MemFSDir(string dirname, MemFSDir parent_dir)
        {
            if (!InvalidChar.CheckValidChar(dirname))
            {
                throw new ArgumentException("Invalid dirname");
            }
            parent = parent_dir;
            name = dirname;
        }

        public override string Name
        {
            get
            {
                return name;
            }
        }

        public override Dir422 Parent
        {
            get
            {
                return parent;
            }
        }

        // add the file into file_list
        public void RegisterFile(MemFSFile file)
        {
            if(! file_list.ContainsKey(file.Name))
                file_list.Add(file.Name,file);
        }

        // add the dir into dir_list
        public void RegisterDir(MemFSDir dir)
        {
            if (!dir_list.ContainsKey(dir.Name))
                dir_list.Add(dir.name,dir);
        }

        // return sub dirs by dir_list
        public override IList<Dir422> GetDirs()
        {
            List<Dir422> list = new List<Dir422>();
            foreach (KeyValuePair<string, MemFSDir> kvp in dir_list)
                list.Add(kvp.Value);
            return list;
        }

        //return files by fild_list
        public override IList<File422> GetFiles()
        {
            List<File422> list = new List<File422>();
            foreach (KeyValuePair<string, MemFSFile> kvp in file_list)
                list.Add(kvp.Value);
            return list;
        }

        // similar strategy, traverse file_list to check contain or not
        public override bool ContainsFile(string fileName, bool recursive)
        {
            if (!InvalidChar.CheckValidChar(fileName))
                return false;
            if (file_list.ContainsKey(fileName))
            {
                return true;
            }
            if (recursive)
            {
                foreach (KeyValuePair<string, MemFSDir> kvp in dir_list)
                {
                    if (kvp.Value.ContainsFile(fileName, recursive))
                        return true;
                }
            }
            return false;
        }

        // similar strategy, traverse dir_list to check contain or not
        public override bool ContainsDir(string dirName, bool recursive)
        {
            if (!InvalidChar.CheckValidChar(dirName))
                return false;
            if (dir_list.ContainsKey(dirName))
            {
                return true;
            }
            if (recursive)
            {
                foreach (KeyValuePair<string, MemFSDir> kvp in dir_list)
                {
                    if (kvp.Value.ContainsDir(dirName, recursive))
                        return true;
                }
            }
            return false;
        }

        // return the file object if exist
        public override File422 GetFile(string fileName)
        {
            if (!InvalidChar.CheckValidChar(fileName))
                return null;
            if (file_list.ContainsKey(fileName))
            {
                return file_list[fileName];
            }
            return null;
        }

        // return the dir object if exist
        public override Dir422 GetDir(string dirName)
        {
            if (!InvalidChar.CheckValidChar(dirName))
                return null;
            if (dir_list.ContainsKey(dirName))
            {
                return dir_list[dirName];
            }
            return null;
        }

        // create dir, need to be consistent to our dir_list and check valid name or not
        public override Dir422 CreateDir(string dirName)
        {
            if (!InvalidChar.CheckValidChar(dirName))
                return null;
            if (dir_list.ContainsKey(dirName))
            {
                return dir_list[dirName];
            }
            MemFSDir dir = new MemFSDir( dirName, this);
            RegisterDir(dir);
            return dir;
        }

        // create file, need to be consistent to our file_list and check valid name or not
        public override File422 CreateFile(string fileName)
        {
            if (!InvalidChar.CheckValidChar(fileName))
                return null;
            if (file_list.ContainsKey(fileName))
            {
                file_list[fileName].Clear();
                return file_list[fileName];
            }
            MemFSFile file = new MemFSFile(fileName, this);
            RegisterFile(file);
            return file;
        }
    } 

    public class MemFSFile: File422
    {
        MemFSDir parent;
        string name;
        byte[] buffer = new byte[1024]; // buffer which holds the content of the file
        List<MemoryStream> stream_list = new List<MemoryStream>();

        //construction
        public MemFSFile(string filename, MemFSDir parentfile)
        {
            if (!InvalidChar.CheckValidChar(filename))
                throw new ArgumentException("Invalid filename");
            parent = parentfile;
            name = filename;
            parent.RegisterFile(this);
        }

        public override Dir422 Parent
        {
            get
            {
                return parent;
            }
        }

        public override string Name
        {
            get
            {
                return name;
            }
        }

        // we need to clear the buffer when we recreate the file
        public void Clear()
        {
            buffer = new byte[1024];
        }

        public override Stream OpenReadOnly()
        {
            // use lock to ensure thread-safe
            lock (stream_list)
            {
                int i = 0;
                // disposed stream has been deleted from stream list, we only need to check the remaining's canwrite property
                while (i != stream_list.Count)
                {
                    if (stream_list[i].CanWrite)
                        return null;
                    i++;
                }
                // initialize a new stream and set its local buffer to the copy of file buffer
                ReadWriteMemoryStream rwms = new ReadWriteMemoryStream();
                rwms.Write(buffer, 0, buffer.Length);
                rwms.Seek(0, SeekOrigin.Begin);
                rwms.SetWrite(false);

                // add this stream and subscribe to it
                stream_list.Add(rwms);
                rwms.PropertyChanged += new PropertyChangedEventHandler(DisposeChange);
                return rwms;
            }
            
        }

        public override Stream OpenReadWrite()
        {
            lock (stream_list)
            {
                int i = 0;
                // disposed stream has been deleted from stream list, we only need to check the remaining's canread, canwrite property
                while (i != stream_list.Count)
                {
                    if (stream_list[i].CanRead || stream_list[i].CanWrite)
                        return null;
                    i++;
                }
                // initialize a new stream and set its local buffer to the copy of file buffer
                ReadWriteMemoryStream rwms = new ReadWriteMemoryStream();
                rwms.Write(buffer, 0, buffer.Length);
                rwms.Seek(0, SeekOrigin.Begin);
                rwms.SetWrite(true);

                // add this stream and subscribe to it
                stream_list.Add(rwms);
                rwms.PropertyChanged += new PropertyChangedEventHandler(DisposeChange);
                return rwms;
            }
        }

        private void DisposeChange(object sender, PropertyChangedEventArgs e)
        {
            ReadWriteMemoryStream rmos = sender as ReadWriteMemoryStream;
            if(rmos != null)
            {
                lock (stream_list)
                {
                    // when dispose, we update the file's buffer, and delete this stream from stream_list
                    buffer = rmos.GetBuffer();
                    for (int i = 0; i < stream_list.Count; i++)
                    {
                        if (object.ReferenceEquals(stream_list[i], rmos))
                        {
                            stream_list.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
        }
    }

    public class ReadWriteMemoryStream: MemoryStream, INotifyPropertyChanged
    {
        bool canwrite = true;
        // use propertychange event to notify the file that the stream is disposed
        public event PropertyChangedEventHandler PropertyChanged;

        

        public override bool CanWrite
        {
            get
            {
                return canwrite;
            }
        }

        // let the file to decide whether it's writable or not
        internal void SetWrite( bool value)
        {
            canwrite = value;
        }

        
        // call the base.write method or throw exception
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (CanWrite)
                base.Write(buffer, offset, count);
            else
                throw new NotSupportedException("don't support write");
        }

        // override the dispose method
        protected override void Dispose(bool disposing)
        {
            canwrite = false;
            PropertyChanged(this, new PropertyChangedEventArgs("stream is disposed"));
            base.Dispose(disposing);
        }

        

        //public override void Flush()
        //{
        //    ms.Flush();
        //}
    }
}
