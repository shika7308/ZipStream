using System;
using System.IO;
using System.IO.Compression;

namespace ZipStream
{
    class Program
    {
        static void Main(string[] args)
        {
            zipTest();

            Console.WriteLine("Hello World!");
            Console.ReadKey();
        }

        static void zipTest()
        {
            var files = new[]
            {
                @"C:\Users\darus\Documents\test\test1.txt",
                @"C:\Users\darus\Documents\test\test2.txt",
                @"C:\Users\darus\Documents\test\test3.txt",
                @"C:\Users\darus\Documents\test\test4.txt",
                @"C:\Users\darus\Documents\test\test5.txt",
                @"C:\Users\darus\Documents\test\test6.txt",
                @"C:\Users\darus\Documents\test\test7.txt",
                @"C:\Users\darus\Documents\test\test8.txt",
                @"C:\Users\darus\Documents\test\test9.txt",
                @"C:\Users\darus\Documents\test\test10.txt",
            };

            using (var output = new FileStream(@"C:\Users\darus\Documents\test\out.zip", FileMode.OpenOrCreate))
            using (var zip = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, true, System.Text.Encoding.UTF8))
            {
                foreach (var file in files)
                {
                    var fi = new FileInfo(file);
                    zip.CreateEntryFromFile(fi.FullName, fi.Name, CompressionLevel.NoCompression);
                }
                zip.Dispose();
            }

            using (var output = new FileStream(@"C:\Users\darus\Documents\test\test1.zip", FileMode.Open))
            using (var zip = new ZipArchiveReader(output))
            {
                var fi = new FileInfo(files[0]);
                //for (var i = 0; i < (1 << 17); i++)
                //{
                //    zip.AddEntity(fi, $"test{i}.txt");
                //}
                using (var st = new MemoryStream())
                {
                    foreach (var name in zip.GetEntities(st))
                    {
                        using (var file = File.OpenWrite($@"C:\Users\darus\Documents\test\test1\{name}"))
                        {
                            var len = (int)st.Position;
                            file.Write(st.GetBuffer(), 0, len);
                        }
                        st.Position = 0;
                    }
                }
            }
        }
    }
}
