using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;

namespace CS422
{
    // filewebservice for supporting file service in the server
    class FilesWebService: WebService
    {
        private readonly FileSys422 r_fs;
        // manually entered content-type dictionary, first string is file suffix as key, and second is web response header 
        private Dictionary<string, string> content_type = new Dictionary<string, string>();
        // web response header
        List<string> content_type1 = new List<string>() { "application/octet-stream", "image/tiff", "video/avi", "application/x-bmp", "text/css", "application/x-msdownload", "application/msword", "application/x-msdownload", "text/html", "application/x-img", "image/jpeg"
        , "application/x-jpg", "video/mpeg4", "video/x-sgi-movie", "audio/mp3", "video/mp4", "application/pdf", "image/png", "application/vnd.ms-powerpoint"
        , "application/vnd.rn-realmedia-vbr", "application/msword", "text/plain", "audio/wav", "video/x-ms-wmv", "text/html", "application/vnd.ms-excel", "text/xml"};
        // file suffix
        List<string> content_type2 = new List<string>() { "*", "tif", "avi", "bmp", "css", "dll", "doc", "exe", "html", "img", "jpeg", "jpg"
        , "m4e", "movie", "mp3", "mp4", "pdf", "png", "ppt", "rmvb", "rtf", "txt", "wav", "wmv", "xhtml","xls","xml"};

        // percent coding for '#'
        string percent_sign = "%25";
        // if request doensn't specify bytes count in range, then we send bytes with count below
        public long length_thres_partial_content = 1024 * 1024 * 10; 

        // constructor, taking file system as parameter
        public FilesWebService(FileSys422 fs)
        {
            r_fs = fs;
            // register the content type to support diffrent file types
            for (int i = 0; i < content_type1.Count; i++)
                content_type.Add(content_type2[i], content_type1[i]);
        }

        // do percent decoding from char to byte
        private byte GetCharValue(char c)
        {
            if (c >= 'A' && c <= 'F')
                return (byte)(c - 'A' + 10);
            else if (c >= '0' && c <= '9')
            {
                return (byte)(c - '0');
            }
            else // if not in range A-F, 0-9, then it's wrong percent encoding, which should be 0x....
                throw new ArgumentException("char is not in correct range A-F, and 0-9");
        }

        // handler for webservice
        public override void Handler(WebRequest req)
        {
            // check URI starting
            if (!req.URI.StartsWith(ServiceURI))
            {
                throw new InvalidOperationException();
            }

            // check URI, if URI == "/files", directly respond with list
            if(req.URI == "/files" || req.URI == "/files/")
            {
                RespondWithList(r_fs.GetRoot(), req);
                return;
            }

            // remove the /files string in URI
            string URI_remaining = req.URI.Substring(ServiceURI.Length);
            if (WebRequest.utf8_enabled) // utf8_enabled means whether we uses utf-8 encoding, which is useful for supporting many characters
            {
                URI_remaining = Encoding.UTF8.GetString(Encoding.ASCII.GetBytes(URI_remaining));
            }
            else
            {

            }
            // do percent decoding
            string uri_decode = "";
            byte[] uri_decode_buffer = new byte[URI_remaining.Length]; // store the original uncoded bytes
            int start = 0;
            for (int index = 0; index < URI_remaining.Length; index++)
            {
                if (URI_remaining[index] == '%') // if we meet a percent, then we do percent decoding 
                {
                    if (index + 2 < URI_remaining.Length) // need to 2 chars for percent decoding
                    {
                        uri_decode_buffer[start++] = (byte) (GetCharValue(URI_remaining[index+1])*16 + GetCharValue(URI_remaining[index + 2]));
                        index += 2;
                    }
                    else
                    {
                        req.WriteNotFoundResponse("<html>File is Not Found</html>");
                        return;
                    }
                }
                else
                {
                    foreach(byte b in Encoding.ASCII.GetBytes(URI_remaining.Substring(index, 1)))
                    {
                        uri_decode_buffer[start++] = b;
                    }
                }
            }

            // change original bytes to string
            if (WebRequest.utf8_enabled)
            {
                uri_decode = Encoding.UTF8.GetString(uri_decode_buffer, 0, start);
            }
            else
            {
                uri_decode = Encoding.ASCII.GetString(uri_decode_buffer, 0, start);
            }
            
            // split based on '/', because '/' is separator in URI
            string[] pieces = uri_decode.Split('/');
            if(null == pieces || pieces.Length == 0)
            {
                req.WriteNotFoundResponse("<html>File is Not Found</html>");
                return;
            }

            // get the root of file system
            var dir = r_fs.GetRoot();
            int i = 0;
            if (pieces[i].Length == 0) i++; // if we recieve /files/abc.txt, then the first piece is empty
            else // if we receive /fileswhatevernoslash.txt, then it's incorrect 
            {
                req.WriteNotFoundResponse("<html>File is Not Found</html>");
                return;
            }
            for( ; i < pieces.Length - 1; i++)  // not foreach because we want to keep the flexibility to verify file or dir
            {
                dir = dir.GetDir(pieces[i]);
                if(dir == null)
                {
                    req.WriteNotFoundResponse("<html>File is Not Found</html>");
                    return;
                }
            }
            // try to get the last piece as a file, then dir... 404 if neither 
            var file = dir.GetFile(pieces[pieces.Length - 1]);
            if(file != null)
            {
                RespondWithFile(file, req);
                return;
            }
            dir = dir.GetDir(pieces[pieces.Length - 1]);
            if(dir == null)
            {
                req.WriteNotFoundResponse("<html>File is Not Found</html>");
                return;
            }
            RespondWithList(dir, req);
        }

        // method for dealing with file request
        private void RespondWithFile(File422 file, WebRequest req)
        {
            // initialized file stream, html template, network stream
            Stream stream = file.OpenReadOnly();
            string Template = null;
            NetworkStream ns = req.NetStream;
            
            // file suffix, filename and starting byte and the count of bytes to send
            string filetype = "";
            string m = file.Name;
            long start = 0, count = stream.Length;

            // get file suffix
            if (m.Contains('.'))
            {
                for (int i = m.Length - 1; i >= 0; i--)
                {
                    if (m[i] != '.')
                        filetype = m[i] + filetype;
                    else
                        break;
                }
            }
            else
            {
                filetype = "*";
            }

            // change charset based on whether we are sending txt
            string txt_charset = (filetype == "txt") ? "; charset = " + Encoding.Default.WebName : "";
            // get contenttype using dictionary
            string contenttype = content_type.ContainsKey(filetype) ? content_type[filetype] : content_type["*"];
            string response = null; // response string
            // do encoding stuff here, like #$%^&*
            byte[] buffer_response = null; // byte[] buffer for encoding response
            bool invalidRangeHeader = true; // indicate whether Range header in request is valid
            long end = 0; // the end byte index from Range header in request
            DateTime time = DateTime.Now;             // Use current time.
            string format = "MMM ddd d HH:mm yyyy";   // Use this format.
            string date = time.ToString(format) + time.Kind; // get the current date
            DateTime lastModified = (file as StdFSFile).GetInfo().LastWriteTime; // get the last modified time
            string lastModi = lastModified.ToString(format) + lastModified.Kind;
            byte[] buffer = null;
            int read = 0;

            // check range header in request
            if (req.header.ContainsKey("Range"))
            {
                string s_tmp = req.header["Range"];
                int index_start = 0;
                // check starting index for staring byte index in Range header
                for (int i = 0; i < s_tmp.Length; i++)
                {
                    if (s_tmp[i] == '=' && i + 1 < s_tmp.Length)
                    {
                        index_start = i + 1;
                    }
                }
                if (index_start > 0)
                {
                    // remove empty char and split by '-' to get starting index and ending index from request header
                    string ss_tmp = s_tmp.Substring(index_start).Replace(" ","");
                    if(ss_tmp != null)
                    {
                        char[] charSeparators = new char[] { '-' };
                        string[] pieces = ss_tmp.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);
                        if(pieces != null && (pieces.Length == 1 || pieces.Length == 2))
                        {
                            // get long type starting byte and ending byte
                            if (long.TryParse(pieces[0], out start))
                            {
                                if(pieces.Length == 2)
                                {
                                    
                                    if (long.TryParse(pieces[1], out end) && end > start)
                                    {
                                        invalidRangeHeader = false;
                                        count = end - start +1;
                                    }
                                }
                                else
                                {
                                    invalidRangeHeader = false;
                                    count = stream.Length - start; // set count of bytes to send
                                }
                            }
                        }
                    }
                }
                else if(s_tmp.Replace(" ", "").Length == 0) // which means the range header is empty
                {
                    // if range header is empty, we need to send the html template below
                    Template = "HTTP/1.1 200 OK\r\n" + "Content-Type: {0}" + txt_charset + "\r\n" + "Accept-Ranges: bytes\r\n" + "Content-Length: {1}\r\n\r\n";
                    response = string.Format(Template, contenttype, stream.Length);
                    if (WebRequest.utf8_enabled)
                    {
                        buffer_response = Encoding.UTF8.GetBytes(response);
                    }
                    else
                    {
                        buffer_response = Encoding.ASCII.GetBytes(response);
                    }

                    ns.Write(buffer_response, 0, buffer_response.Length);
                    count = stream.Length;
                    buffer = new byte[1024];
                    read = stream.Read(buffer, 0, buffer.Length);

                    while (count > 0) // send bytes until finished all the counts
                    {
                        try
                        {
                            ns.Write(buffer, 0, read);
                            if (ns.DataAvailable)
                            {

                            }
                        }
                        catch (Exception e)
                        {
                            ns.Dispose();
                            stream.Dispose();
                            return;
                        }
                        count -= read;
                        read = stream.Read(buffer, 0, buffer.Length);
                    }
                    stream.Dispose();
                    ns.Dispose();
                    return;
                }
                // if not valid range header, send not found response and return
                if (invalidRangeHeader)
                {
                    req.WriteNotFoundResponse("<html>File is Not Found</html>");
                    return;
                }

                // if range header is valid, we need to send the html template below
                Template = "HTTP/1.1 206 Partial Content\r\n" + "Date:" + date + "\r\n" + "Last-Modified:" +
                    lastModi + "\r\n" + "Content-Type: {0}" + txt_charset + "\r\n" + 
                    "Content-Length: {1}\r\n" + "Accept-Ranges: bytes\r\n" + "Content-Range: bytes " + start + '-'
                    + (start + count - 1) + '/' + (stream.Length) + "\r\n" + "\r\n";
            }
            else
            {
                // if range header is not contained, we need to send the html template below
                Template = "HTTP/1.1 200 OK\r\n" + "Content-Type: {0}" + txt_charset + "\r\n" + "Content-Length: {1}\r\n\r\n";
                count = stream.Length;
            }

            if (filetype != "mp4")
                invalidRangeHeader = false;

            // get response string and the buffer
            response = string.Format(Template, contenttype, count);
            if (WebRequest.utf8_enabled)
            {
                buffer_response = Encoding.UTF8.GetBytes(response);
            }
            else
            {
                buffer_response = Encoding.ASCII.GetBytes(response);
            }

            // write response
            ns.Write(buffer_response, 0, buffer_response.Length);

            buffer = new byte[1024];

            // set the starting index for sending bytes
            stream.Seek(start, SeekOrigin.Begin);// adjust the start point to make it appropriate for start byte position
            read = stream.Read(buffer, 0, buffer.Length);

            // debug use only below
            if ( !req.header.ContainsKey("Range") && filetype == "mp4")
            {
                //count = read;
            }//&& !invalidRangeHeader)
            while (count > 0 ) // send bytes until finished all the counts
            {
                try
                {
                    ns.Write(buffer, 0, read);
                    if (ns.DataAvailable)
                    {

                    }
                }
                catch(Exception e)
                {
                    ns.Dispose();
                    stream.Dispose();
                    return;
                }
                count -= read;
                read = stream.Read(buffer, 0, buffer.Length);
            }

            
            ns.Dispose();
            stream.Dispose();
        }

        // method for response with file and dir list
        private void RespondWithList(Dir422 dir, WebRequest req)
        {
            // set html as a string builder
            var html = new System.Text.StringBuilder("<html>");

            // initialize dirpath and traverse to the null parent to set dirpath
            string dirPath = "";
            Dir422 temp = dir;
            while (temp.Parent != null)
            {
                string s_tmp = temp.Name.Replace("%", percent_sign); // need to encode all the % char in the string
                dirPath = s_tmp + "/" + dirPath;
                temp = temp.Parent;
            }
            dirPath = "/files/" + dirPath;

            // append folder section
            html.Append("<h1> Folders </h1> <br>");

            
            foreach (var direc in dir.GetDirs())
            {
                string s_tmp = direc.Name.Replace("%", percent_sign); // need to encode all the % char in the string
                string href = dirPath + s_tmp;
                html.AppendFormat(
                    "<a href=\"{0}\">{1}</a>     <br>",
                    href, direc.Name); // need to notice that the strings are encoded
            }

            // append all the files
            html.Append("<h1> Files </h1>  <br>");
            foreach (var file in dir.GetFiles())
            {
                string s_tmp = file.Name.Replace("%", percent_sign); // need to encode all the % char in the string
                string href = dirPath + s_tmp;
                html.AppendFormat(
                    "<a href=\"{0}\">{1}</a>     <br>", 
                    href, file.Name); // need to notice that the strings are encoded
            }

            html.AppendLine("</html>");
            req.WriteHTMLResponse(html.ToString());

        }

        public override string ServiceURI
        {
            get
            {
                return "/files";
            }
        }
    }
}


//below is the original code for processing percent decoding using dictionary
//for (int index = 0; index < URI_remaining.Length; index++)
//{
//    if (URI_remaining[index] == '%')
//    {
//        if (index + 2 < URI_remaining.Length && decoding.ContainsKey(URI_remaining.Substring(index, 3)))
//        {
//            uri_decode += decoding[URI_remaining.Substring(index, 3)];
//            index += 2;
//        }
//        else
//        {
//            req.WriteNotFoundResponse("<html>File is Not Found</html>");
//            return;
//        }
//    }
//    else
//    {
//        uri_decode += URI_remaining[index];
//    }
//}