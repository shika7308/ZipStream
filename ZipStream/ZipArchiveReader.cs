using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZipStream
{
    public class ZipArchiveReader : IDisposable
    {
        static ZipArchiveReader()
        {
            make_crc_table();
        }

        Stream input;

        public ZipArchiveReader(Stream input)
        {
            this.input = input;
        }

        public void Dispose()
        {
        }

        public IEnumerable<string> GetEntities(MemoryStream output)
        {
            var buf = new byte[1 << 16];
            var head = new byte[30];
            for (; ; )
            {
                input.Read(head);
                if (BitConverter.ToInt32(head, 0) != 0x04034b50)
                    yield break;

                var dataCrc = (ulong)BitConverter.ToUInt32(head, 14);
                var len = BitConverter.ToUInt32(head, 18);
                var nameLen = BitConverter.ToUInt16(head, 26);
                var extLen = BitConverter.ToInt16(head, 28);
                input.Read(buf, 0, nameLen);
                var fileName = Encoding.UTF8.GetString(buf, 0, nameLen);

                var crc = 0UL;
                var sum = 0L;
                int l, restLen;
                for (; ; )
                {
                    restLen = (int)(len - sum);
                    if (restLen < buf.Length)
                        l = input.Read(buf, 0, restLen);
                    else
                        l = input.Read(buf, 0, buf.Length);
                    crc = update_crc(crc, buf, l);
                    output.Write(buf, 0, l);
                    sum += l;
                    if (sum == len)
                        break;
                }
                if (crc != dataCrc)
                    throw new Exception("File was broken");

                yield return fileName;
            }
        }

        /* Table of CRCs of all 8-bit messages. */
        static ulong[] crc_table = new ulong[256];

        /* Make the table for a fast CRC. */
        static void make_crc_table()
        {
            ulong c;
            int n, k;
            for (n = 0; n < 256; n++)
            {
                c = (ulong)n;
                for (k = 0; k < 8; k++)
                {
                    if ((c & 1) == 1)
                    {
                        c = 0xedb88320L ^ (c >> 1);
                    }
                    else
                    {
                        c = c >> 1;
                    }
                }
                crc_table[n] = c;
            }
        }

        /* Update a running crc with the bytes buf[0..len-1] and return
         * the updated crc. The crc should be initialized to zero.
         * Pre- and post-conditioning (one's complement) is performed
         * within this function so it shouldn't be done by the caller.
         * Usage example: 
         * 
         *   unsigned long crc = 0L; 
         *   
         *   while (read_buffer(buffer, length) != EOF)
         *   {
         *      crc = update_crc(crc, buffer, length);
         *   }
         *   if (crc != original_crc) error();
         */

        ulong update_crc(ulong crc, byte[] buffer, int len)
        {
            ulong c = crc ^ 0xffffffffL;
            int n;

            unsafe
            {
                fixed (ulong* table = crc_table)
                fixed (byte* b = buffer)
                {
                    var buf = b;
                    for (n = 0; n < len; n++)
                    {
                        c = table[(c ^ *buf++) & 0xff] ^ (c >> 8);
                    }
                }
            }
            return c ^ 0xffffffffL;
        }

        /* Return the CRC of the bytes buf[0..len-1]. */
        //ulong crc(byte[] buf)
        //{
        //    return update_crc(0L, buf);
        //}
    }
}
