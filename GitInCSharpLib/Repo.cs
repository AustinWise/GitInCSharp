using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            ObjectId objId = ObjectId.Parse(objectFi.Directory.Name + objectFi.Name);

            //TODO: don't open twice
            using (var fs = objectFi.OpenRead())
            {
                using (var inflater = new InflaterInputStream(fs))
                {
                    var sha = SHA1.Create();
                    if (!objId.Equals(new ObjectId(sha.ComputeHash(inflater))))
                        throw new Exception("Hash is not right!");
                }
            }

            using (var fs = objectFi.OpenRead())
            {
                //TODO: apply some not-invented-here syndrom by reimplmenting inflate
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

        class PersonTime
        {
            static DateTime sEpoc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            PersonTime(string name, string email, DateTime time)
            {
                this.Name = name;
                this.Email = email;
                this.Time = time;
            }

            public string Name { get; }
            public string Email { get; }
            public DateTime Time { get; }

            public override string ToString()
            {
                return $"{Name} <{Email}> {Time:s}";
            }

            public static PersonTime Parse(string str)
            {
                if (str == null)
                    throw new ArgumentNullException(nameof(str));

                int openAngleBracket = str.IndexOf('<');
                if (openAngleBracket == -1)
                    throw new ArgumentNullException("Missing angle bracket.");
                int closeAngleBracket = str.IndexOf('>', openAngleBracket + 1);
                if (closeAngleBracket == -1)
                    throw new Exception("Missing close angle breacket.");
                if (str[openAngleBracket - 1] != ' ' || str[closeAngleBracket + 1] != ' ')
                    throw new Exception("Missing spaces around email.");
                int spaceNdx = str.IndexOf(' ', closeAngleBracket + 2);
                if (spaceNdx == -1)
                    throw new Exception("Could not find space between timespace and time zone.");

                string name = str.Substring(0, openAngleBracket - 1);
                string email = str.Substring(openAngleBracket + 1, closeAngleBracket - openAngleBracket - 1);
                string timestampStr = str.Substring(closeAngleBracket + 2, spaceNdx - closeAngleBracket - 2);
                string zoneOffsetStr = str.Substring(spaceNdx + 1);
                DateTime time = sEpoc.AddSeconds(int.Parse(timestampStr, CultureInfo.InvariantCulture));
                //TODO: do something with the time zone offset?
                return new PersonTime(name, email, time);
            }
        }

        void inspectCommit(byte[] bytes)
        {
            const string TREE_STR = "tree ";
            const string PARENT_STR = "parent ";
            const string AUTHOR_STR = "author ";
            const string COMMITTER_STR = "committer ";

            List<ObjectId> parents = new List<ObjectId>();
            ObjectId tree = new ObjectId();
            PersonTime author = null;
            PersonTime committer = null;

            string all = Encoding.UTF8.GetString(bytes);

            var mem = new MemoryStream(bytes);
            while (true)
            {
                string line = readAsciiTo(mem, '\n');
                if (line.StartsWith(TREE_STR))
                {
                    if (!tree.IsZero)
                        throw new Exception("Multiple trees found.");
                    tree = ObjectId.Parse(line.Substring(TREE_STR.Length));
                }
                else if (line.StartsWith(PARENT_STR))
                {
                    parents.Add(ObjectId.Parse(line.Substring(PARENT_STR.Length)));
                }
                else if (line.StartsWith(AUTHOR_STR))
                {
                    if (author != null)
                        throw new Exception("Multiple authors.");
                    author = PersonTime.Parse(line.Substring(AUTHOR_STR.Length));
                }
                else if (line.StartsWith(COMMITTER_STR))
                {
                    if (committer != null)
                        throw new Exception("Multiple committers.");
                    committer = PersonTime.Parse(line.Substring(COMMITTER_STR.Length));
                }

                if (line.Length == 0)
                    break;
            }

            string commitMessage = Encoding.UTF8.GetString(bytes, (int)mem.Position, (int)(mem.Length - mem.Position));
            Console.WriteLine("\tAuthor: {0}", author);
            Console.WriteLine("\tCommitter: {0}", committer);
            Console.WriteLine("\tTree: {0}", tree);
            foreach (var parent in parents)
            {
                Console.WriteLine("\tParent: {0}", parent.IdStr);
            }
        }

        void inspectTree(byte[] bytes)
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
