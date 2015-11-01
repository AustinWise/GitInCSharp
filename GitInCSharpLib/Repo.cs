using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Austin.GitInCSharpLib
{
    public class Repo
    {
        public Repo(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (!Directory.Exists(path))
                throw new ArgumentException(nameof(path), "Directory does not exist.");
            string gitDir = Path.Combine(path, ".git");
            if (!Directory.Exists(gitDir))
                throw new ArgumentException(nameof(path), ".git directory not found.");

            string objectDir = Path.Combine(gitDir, "objects");
            if (!Directory.Exists(objectDir))
                throw new InvalidDataException("Missing 'objects' dir in .git dir.");

            foreach (var subDir in new DirectoryInfo(objectDir).GetDirectories().Where(d => d.Name.Length == 2))
            {
                foreach (var o in subDir.EnumerateFiles())
                {
                    inspectFile(o);
                }
            }
        }

        //TODO: probably optimize this, looks like it could alloc a lot less GC memory.
        static string readAsciiTo(Stream s, char ch)
        {
            var bytes = new List<byte>();
            while (true)
            {
                int b = s.ReadByte();
                if (b == -1)
                    throw new Exception("Unexpected EOF.");
                if ((char)b == ch)
                    break;
                bytes.Add((byte)b);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        void inspectFile(FileInfo objectFi)
        {
            //TODO: apply some not-invented-here syndrom by reimplmenting inflate
            //TODO: make sure fi.OpenRead uses FileShare.Read
            using (var fs = objectFi.OpenRead())
            {
                using (var inflater = new InflaterInputStream(fs))
                {
                    string tag = readAsciiTo(inflater, ' ');
                    string sizeStr = readAsciiTo(inflater, '\0');
                    Console.WriteLine("{0}: {1}", tag, sizeStr);
                }
            }
        }
    }
}
