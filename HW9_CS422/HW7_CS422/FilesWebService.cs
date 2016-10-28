using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;

namespace CS422
{
    class FilesWebService: WebService
    {
        private readonly FileSys422 r_fs;
        private Dictionary<string, string> content_type = new Dictionary<string, string>();
        List<string> content_type1 = new List<string>() { "application/octet-stream", "image/tiff", "video/avi", "application/x-bmp", "text/css", "application/x-msdownload", "application/msword", "application/x-msdownload", "text/html", "application/x-img", "image/jpeg"
        , "application/x-jpg", "video/mpeg4", "video/x-sgi-movie", "audio/mp3", "video/mpeg4", "application/pdf", "image/png", "application/vnd.ms-powerpoint"
        , "application/vnd.rn-realmedia-vbr", "application/msword", "text/plain", "audio/wav", "video/x-ms-wmv", "text/html", "application/vnd.ms-excel", "text/xml"};
        List<string> content_type2 = new List<string>() { "*", "tif", "avi", "bmp", "css", "dll", "doc", "exe", "html", "img", "jpeg", "jpg"
        , "m4e", "movie", "mp3", "mp4", "pdf", "png", "ppt", "rmvb", "rtf", "txt", "wav", "wmv", "xhtml","xls","xml"};

        //previous i use dictionary for encoding and decoding, but the new method makes it useless
        //Dictionary<char, string> encoding = new Dictionary<char, string>()
        //{
        //     {' ', "%20"}, {'!',"%21" }, {'$', "%24" }, {'&',"%26" },  { (char)27,"%27"}, { '(' ,"%28"}, { ')',"%29"}, { '*',"%2A"}, { '+',"%2B"}, { ',',"%2C"}, { '/',"%2F"}, { ':',"%3A"}, { ';',"%3B"}, { '=',"%3D"}
        //    , { '?',"%3F"}, { '@',"%40"}, { '[',"%5B"}, { ']',"%5D"}
        //};
        //Dictionary<string, char> decoding = new Dictionary<string, char>();
        string percent_sign = "%25";
        public long length_thres_partial_content = 1024 * 1024 * 10; // if file is bigger than 10 MB, we do partial content

        public FilesWebService(FileSys422 fs)
        {
            r_fs = fs;
            for (int i = 0; i < content_type1.Count; i++)
                content_type.Add(content_type2[i], content_type1[i]);

            //foreach(KeyValuePair<char,string> kvp in encoding)
            //    decoding.Add(kvp.Value, kvp.Key);
        }

        private byte GetCharValue(char c)
        {
            if (c >= 'A' && c <= 'F')
                return (byte)(c - 'A' + 10);
            else if (c >= '0' && c <= '9')
            {
                return (byte)(c - '0');
            }
            else
                throw new ArgumentException("char is not in correct range A-F, and 0-9");
        }

        public override void Handler(WebRequest req)
        {
            
            if (!req.URI.StartsWith(ServiceURI))
            {
                throw new InvalidOperationException();
            }

            if(req.URI == "/files" || req.URI == "/files/")
            {
                RespondWithList(r_fs.GetRoot(), req);
                return;
            }

            string URI_remaining = req.URI.Substring(ServiceURI.Length);
            if (WebRequest.utf8_enabled)
            {
                URI_remaining = Encoding.UTF8.GetString(Encoding.ASCII.GetBytes(URI_remaining));
            }
            else
            {

            }
            string uri_decode = "";
            byte[] uri_decode_buffer = new byte[URI_remaining.Length];
            int start = 0;
            for (int index = 0; index < URI_remaining.Length; index++)
            {
                if (URI_remaining[index] == '%')
                {
                    if (index + 2 < URI_remaining.Length)
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

            if (WebRequest.utf8_enabled)
            {
                uri_decode = Encoding.UTF8.GetString(uri_decode_buffer, 0, start);
            }
            else
            {
                uri_decode = Encoding.ASCII.GetString(uri_decode_buffer, 0, start);
            }
            

            string[] pieces = uri_decode.Split('/');
            if(null == pieces || pieces.Length == 0)
            {
                req.WriteNotFoundResponse("<html>File is Not Found</html>");
                return;
            }
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

        private void RespondWithFile(File422 file, WebRequest req)
        {
            Stream stream = file.OpenReadOnly();
            string Template = null;
            NetworkStream ns = req.NetStream;
            string filetype = "";
            string m = file.Name;
            long start = 0, count = stream.Length;

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

            string txt_charset = (filetype == "txt") ? "; charset = " + Encoding.Default.WebName : "";
            string contenttype = content_type.ContainsKey(filetype) ? content_type[filetype] : content_type["*"];
            string response = null;
            // do encoding stuff here, like #$%^&*
            byte[] buffer_response = null;
            bool invalidRangeHeader = true;

            if (req.header.ContainsKey("Range"))
            {
                string s_tmp = req.header["Range"];
                int index_start = 0;
                for (int i = 0; i < s_tmp.Length; i++)
                {
                    if (s_tmp[i] == '=' && i + 1 < s_tmp.Length)
                    {
                        index_start = i + 1;
                    }
                }
                if (index_start > 0)
                {
                    string ss_tmp = s_tmp.Substring(index_start).Replace(" ","");
                    if(ss_tmp != null)
                    {
                        string[] pieces = ss_tmp.Split('-');
                        if(pieces != null && (pieces.Length == 1 || pieces.Length == 2))
                        {
                            if (long.TryParse(pieces[0], out start))
                            {
                                if(pieces.Length == 2)
                                {
                                    long end = 0;
                                    if (long.TryParse(pieces[1], out end) && end > start)
                                    {
                                        invalidRangeHeader = false;
                                        count = end - start;
                                    }
                                }
                                else
                                {
                                    invalidRangeHeader = false;
                                    count = length_thres_partial_content;
                                }
                            }
                        }
                    }
                }
                else if(s_tmp.Replace(" ", "").Length == 0) // which means the range header is empty
                {
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
                    stream.Dispose();
                    ns.Dispose();
                    return;
                }

                if (invalidRangeHeader)
                {
                    req.WriteNotFoundResponse("<html>File is Not Found</html>");
                    return;
                }
                
                Template = "HTTP/1.1 206 Partial Content\r\n" + "Content-Type: {0}" + txt_charset + "\r\n" + 
                    "Content-Length: {1}\r\n" + "Accept-Ranges: bytes\r\n" + "Content-Range: bytes " + start + '-'
                    + (start + count) + '/' + (start + count + 1) + "\r\n " + "\r\n";
            }
            else
            {
                Template = "HTTP/1.1 200 OK\r\n" + "Content-Type: {0}" + txt_charset + "\r\n" + "Content-Length: {1}\r\n\r\n";
                count = stream.Length;
            }

            response = string.Format(Template, contenttype, count);
            if (WebRequest.utf8_enabled)
            {
                buffer_response = Encoding.UTF8.GetBytes(response);
            }
            else
            {
                buffer_response = Encoding.ASCII.GetBytes(response);
            }


            ns.Write(buffer_response, 0, buffer_response.Length);

            byte[] buffer = new byte[1024];
            
            int read = stream.Read(buffer, 0, buffer.Length);
            // adjust the start point to make it appropriate for start byte position
            while (read < start)
            {
                start -= read;
                read = stream.Read(buffer, 0, buffer.Length);
            }

            //int buffer_start = 0;
            while(count > 0)
            {
                ns.Write(buffer, (int)start, read);
                start = 0;
                count -= read;
                read = stream.Read(buffer, 0, buffer.Length);
            }

            stream.Dispose();
            ns.Dispose();

        }

        private void RespondWithList(Dir422 dir, WebRequest req)
        {
            var html = new System.Text.StringBuilder("<html>");

            string dirPath = "";
            Dir422 temp = dir;
            while (temp.Parent != null)
            {
                string s_tmp = temp.Name.Replace("%", percent_sign); // need to encode all the % char in the string
                dirPath = s_tmp + "/" + dirPath;
                temp = temp.Parent;
            }
            dirPath = "/files/" + dirPath;

            html.Append("<h1> Folders </h1> <br>");

            foreach (var direc in dir.GetDirs())
            {
                string s_tmp = direc.Name.Replace("%", percent_sign); // need to encode all the % char in the string
                string href = dirPath + s_tmp;
                html.AppendFormat(
                    "<a href=\"{0}\">{1}</a>     <br>",
                    href, direc.Name); // need to notice that the strings are encoded
            }

            //TODO
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