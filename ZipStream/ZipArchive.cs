using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ZipStream
{
    public class ZipArchive : IDisposable
    {
        static ZipArchive()
        {
            make_crc_table();
        }

        readonly MemoryStream buffer = new MemoryStream();
        readonly Stream output;
        readonly List<(string name, uint len, uint crc, ulong pos)> headers = new List<(string name, uint len, uint crc, ulong pos)>();
        ulong position;
        ulong fileCount;
        ulong cd_start_position;

        public ZipArchive(Stream output)
        {
            this.output = output;
        }

        public void Dispose()
        {
            foreach (var x in headers)
                WriteCentralFileHeaderTo(x.name, x.len, x.crc, x.pos);
            WriteTail();
            buffer.Dispose();
        }

        public void AddEntity(FileInfo file, string internalPath = null)
        {
            if (string.IsNullOrEmpty(internalPath))
                internalPath = file.Name;

            //LocalFileHeader.WriteTo(buffer, internalPath, (int)file.Length, 0);
            using (var input = new FileStream(file.FullName, FileMode.Open))
            {
                input.CopyTo(buffer);
            }
            fileCount++;
            var buf = buffer.GetBuffer();
            var crc = update_crc(0L, buf, (int)buffer.Position);
            WriteLocalFileHeaderTo(internalPath, (uint)file.Length, (uint)crc);
            output.Write(buf, 0, (int)buffer.Position);
            position += (ulong)buffer.Position;
            buffer.Position = 0;
        }

        byte[] fileNameBuffer = new byte[2 << 16];
        void WriteLocalFileHeaderTo(string fileName, uint length, uint crc32)
        {
            headers.Add((fileName, length, crc32, position));

            Span<byte> crc = stackalloc byte[4];
            BitConverter.TryWriteBytes(crc, crc32);
            Span<byte> len = stackalloc byte[4];
            BitConverter.TryWriteBytes(len, length);
            var fileNameLen = (short)Encoding.UTF8.GetBytes(fileName, 0, fileName.Length, fileNameBuffer, 0);
            Span<byte> fnLen = stackalloc byte[2];
            BitConverter.TryWriteBytes(fnLen, fileNameLen);
            ReadOnlySpan<byte> head = stackalloc byte[]
            {
                0x50,
                0x4b,
                0x03,
                0x04, // signature
                0x0a,
                0x00, // version
                0x00,
                0x08, // flags
                0x00,
                0x00, // method
                0x00,
                0x00, // time
                0x00,
                0x00, // date
                crc[0],
                crc[1],
                crc[2],
                crc[3],
                len[0],
                len[1],
                len[2],
                len[3],
                len[0],
                len[1],
                len[2],
                len[3],
                fnLen[0],
                fnLen[1],
                0x00,
                0x00,
            };

            output.Write(head);
            position += (ulong)head.Length;
            output.Write(fileNameBuffer, 0, fileNameLen);
            position += (ulong)fileNameLen;
        }

        void WriteCentralFileHeaderTo(string fileName, uint length, uint crc32, ulong headerOffset)
        {
            if (cd_start_position == 0)
                cd_start_position = position;
            Span<byte> crc = stackalloc byte[4];
            BitConverter.TryWriteBytes(crc, crc32);
            Span<byte> len = stackalloc byte[4];
            BitConverter.TryWriteBytes(len, length);
            var fileNameLen = (short)Encoding.UTF8.GetBytes(fileName, 0, fileName.Length, fileNameBuffer, 0);
            Span<byte> fnLen = stackalloc byte[2];
            BitConverter.TryWriteBytes(fnLen, fileNameLen);
            ReadOnlySpan<byte> head = stackalloc byte[]
            {
                0x50,
                0x4b,
                0x01,
                0x02, // signature
                0x3f,
                0x00, // version
                0x2d,
                0x00, // need to extract
                0x00,
                0x08, // flags
                0x00,
                0x00, // method
                0x00,
                0x00, // time
                0x00,
                0x00, // date
                crc[0],
                crc[1],
                crc[2],
                crc[3],
                len[0],
                len[1],
                len[2],
                len[3],
                len[0],
                len[1],
                len[2],
                len[3],
                fnLen[0],
                fnLen[1],
                0x0c,
                0x00, // extension field length
                0x00,
                0x00, // file comment length
                0x00,
                0x00, // disc number start
                0x00,
                0x00, // internal file attributes
                0x20,
                0x00,
                0x00,
                0x00, // external file attributes
                0xff,
                0xff,
                0xff,
                0xff, // relative offset of local header
            };

            output.Write(head);
            position += (ulong)head.Length;
            output.Write(fileNameBuffer, 0, fileNameLen);
            position += (ulong)fileNameLen;

            Span<byte> offset = stackalloc byte[8];
            BitConverter.TryWriteBytes(offset, headerOffset);
            output.WriteByte(0x01);
            output.WriteByte(0x00);
            output.WriteByte(0x08);
            output.WriteByte(0x00);
            position += 4;
            output.Write(offset);
            position += (ulong)offset.Length;
        }

        void WriteTail()
        {
            Span<byte> cnt = stackalloc byte[8];
            BitConverter.TryWriteBytes(cnt, fileCount);
            Span<byte> cd = stackalloc byte[8];
            BitConverter.TryWriteBytes(cd, cd_start_position);
            Span<byte> cd_size = stackalloc byte[8];
            BitConverter.TryWriteBytes(cd_size, position - cd_start_position);
            Span<byte> head = stackalloc byte[]
            {
                0x50,
                0x4b,
                0x06,
                0x06, // ZIP64 EOCD signature
                0x2c,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00, // size of ZIP64 EOCD-REC (SizeOfFixedFields + SizeOfVariableData – 12)
                0x2d,
                0x00, // version made by
                0x2d,
                0x00, // version needed to extract
                0x00,
                0x00,
                0x00,
                0x00, // number of this disk
                0x00,
                0x00,
                0x00,
                0x00, // number of the disk with the start of the central directory
                cnt[0], cnt[1], cnt[2], cnt[3], cnt[4], cnt[5], cnt[6], cnt[7], // total number of entries in the central directory on this disk
                cnt[0],
                cnt[1],
                cnt[2],
                cnt[3],
                cnt[4],
                cnt[5],
                cnt[6],
                cnt[7],// total number of entries in the central directory
                // size of the central directory
            };

            var eocd_rec = position;
            output.Write(head);
            output.Write(cd_size);
            output.Write(cd);
            position += (ulong)(head.Length + cd_size.Length + cd.Length);

            // ZIP64 end of central directory locater
            BitConverter.TryWriteBytes(cd, eocd_rec);
            head = stackalloc byte[]
            {
                0x50,
                0x4b,
                0x06,
                0x07, // ZIP64 EOCD-LOC signature
                0x00,
                0x00,
                0x00,
                0x00,// number of the disk with the start of the zip64 end of central directory
            };
            output.Write(head);
            output.Write(cd);
            output.WriteByte(0x01);
            output.WriteByte(0x00);
            output.WriteByte(0x00);
            output.WriteByte(0x00);
            position += (ulong)(head.Length + cd.Length + 4);

            head = stackalloc byte[]
            {
                0x50,
                0x4b,
                0x05,
                0x06, // EOCD signature
                0x00,
                0x00, // number of this disk
                0x00,
                0x00, // number of the disk with the start of the central directory
            };
            var num = cnt.Slice(4);
            var size = cd_size.Slice(4);
            output.Write(head);
            output.Write(num);
            output.Write(size);
            size[0] = 0xff;
            size[1] = 0xff;
            size[2] = 0xff;
            size[3] = 0xff;
            output.Write(size);
            output.Write(size);
            output.WriteByte(0);
            output.WriteByte(0);
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
                fixed (byte* b = buffer)
                {
                    var buf = b;
                    for (n = 0; n < len; n++)
                    {
                        c = crc_table[(c ^ *buf++) & 0xff] ^ (c >> 8);
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
