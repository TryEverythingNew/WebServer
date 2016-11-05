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
    // this stream is especially designed for combining memory stream and network stream
    public class ConcatStream: Stream   
    {
        public Stream s1, s2;
        int init_way;// indicates the way of construction
        long length;
        long position = 0;

        // first constructor
        public ConcatStream(Stream first, Stream second)
        {
            
            if (first.Length >= 0)
            {
                s1 = first;
                s1.Position = 0;
                s2 = second;
            }
            init_way = 1;
        }

        //second constructor
        public ConcatStream(Stream first, Stream second, long fixedLength)
        {
            if (first.Length >= 0)
            {
                s1 = first;
                s1.Position = 0;
                s2 = second;
            }
            length = fixedLength;
            //if(length < first.Length)   // the fixed body length is less than memory stream's length
            //{
            //    throw new NotSupportedException();
            //}
            init_way = 2;
        }

        public override bool CanRead
        {
            get
            {
                return (s1.CanRead && s2.CanRead);
            }
        }

        public override bool CanSeek
        {
            get
            {
                return (s1.CanSeek && s2.CanSeek);
            }
        }

        public override bool CanWrite
        {
            get
            {
                return (s1.CanWrite && s2.CanWrite);
            }
        }

        public override long Length // depends on the way of construction, we output the length
        {
            get
            {
                if(init_way == 2)
                {
                    return length;
                }
                else
                {
                    return s1.Length + s2.Length;
                }
            }
        }

        public override long Position
        {
            get
            {
                return position;
            }

            set
            {
                if(CanSeek)
                    position = value;
                else
                    throw new NotImplementedException();    // not allowed to set position if it is not seekable
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (s1.CanSeek) // if stream 1 is seekable, we set s1's position
            {
                if (s1.Length <= position)
                {
                    s1.Seek(0, SeekOrigin.End);
                    if (s2.CanSeek) // if canseek, we set the s2's position
                    {
                        s2.Seek(position - s1.Length, SeekOrigin.Begin);
                    }
                }
                else
                {
                    s1.Position = position;
                    if (s2.CanSeek) // if canseek, we set the s2's position
                    {
                        s2.Seek( 0, SeekOrigin.Begin);
                    }
                } 
            }
            int readcount1 = s1.Read(buffer, offset, count);
            int readcount2 = 0;
            if(readcount1 < count) // read both streams if first stream is not enough
            {
                readcount2 = s2.Read(buffer,offset + readcount1, count - readcount1);
            }
            position += readcount1 + readcount2;
            return readcount1 + readcount2;
            
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (CanSeek) // if canseek, we set the positions
            {
                long pos_origin = (long)origin;
                long start = 0;
                switch (pos_origin) // set positions depend on origin to decide the start
                {
                    case 0 : start = 0; break;
                    case 1 : start = Position; break;
                    case 2 : start = Length; break;
                }
                long pos = start + offset;
                if(pos >= s1.Length)
                {
                    s1.Position = s1.Length;
                    
                    // set the position of 2nd stream, after I search the documentation, it's allowed to seek outside the length of stream
                    s2.Position = pos - s1.Length;
                }
                else
                {
                    s1.Position = pos;
                    s2.Position = 0;
                }
                return pos;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count) // write function with several conditions to consider
        {
            if (s1.CanSeek)
            {
                if (s1.Length <= position)
                {
                    s1.Seek(0, SeekOrigin.End);
                    if (s2.CanSeek) // if canseek, we set the s2's position
                    {
                        s2.Seek(position - s1.Length, SeekOrigin.Begin);
                    }
                }
                else
                {
                    s1.Position = position;
                    if (s2.CanSeek) // if canseek, we set the s2's position
                    {
                        s2.Seek(0, SeekOrigin.Begin);
                    }
                }
            }
            if (CanWrite)
            {
                if (position < s1.Length) // we can write to first stream if first stream's position is not at end
                {
                    if ((s1.Length - position) < count)
                    {
                        s1.Write(buffer, offset, (int)(s1.Length - position)); // there's a cast here
                        s2.Write(buffer, offset + (int)(s1.Length - position), count - (int)(s1.Length - position));
                    }
                    else
                    {
                        s1.Write(buffer, offset, count);
                    }
                    position += count;
                }
                else  // we have to write to second stream if we could seek second stream or second stream's position is lucky to be equal to the correct position
                {
                    if (CanSeek) // if canseek, we need to seek stream s2 to the proper position and then write
                    {
                        s2.Seek(position - s1.Length, SeekOrigin.Begin);
                        s2.Write(buffer, offset, count);
                        position += count;
                    }
                    else // we can write to second stream only if position is correct 
                    {
                        if (position == (s1.Length + s2.Position))
                        {
                            s2.Write(buffer, offset, count);
                            position += count;
                        }
                        else
                            throw new NotSupportedException();
                    }
                }

            }
            else
                throw new NotSupportedException();
            
        }
    }
}
