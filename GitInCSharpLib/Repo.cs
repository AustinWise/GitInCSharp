using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        byte[] readObjectContents(Stream s, int size)
        {
            byte[] buf = new byte[size];
            if (s.Read(buf, 0, size) != size)
                throw new Exception("Could not read it all.");
            return buf;
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
                    int size = int.Parse(readAsciiTo(inflater, '\0'), NumberStyles.None, CultureInfo.InvariantCulture);
                    Console.WriteLine("{0}: {1}", tag, size);

                    switch (tag)
                    {
                        case "blob":
                            break;
                        case "commit":
                            inspectCommit(readObjectContents(inflater, size));
                            break;
                        case "tree":
                            inspectTree(readObjectContents(inflater, size));
                            break;
                        default:
                            throw new Exception("Unrecognized object type: " + tag);
                    }
                }
            }
        }

        void inspectCommit(byte[] bytes)
        {
        }

        unsafe void inspectTree(byte[] bytes)
        {
            var mem = new MemoryStream(bytes);
            while (true)
            {
                int mode = Convert.ToInt32(readAsciiTo(mem, ' '), 8);
                string fileName = readAsciiTo(mem, '\0'); //TODO: UTF-8 file names
                ObjectId id = ObjectId.ReadFromStream(mem);
                Console.WriteLine("\t{0,6} {1} {2}", Convert.ToString(mode, 8), id.IdStr, fileName);

                if (mem.Position == bytes.Length)
                    break;
            }
        }
    }
}
