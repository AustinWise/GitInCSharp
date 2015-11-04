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
        readonly List<PackFile> mPackFiles = new List<PackFile>();
        readonly DirectoryInfo mObjectDir;

        public Repo(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (!Directory.Exists(path))
                throw new ArgumentException(nameof(path), "Directory does not exist.");
            string gitDir = Path.Combine(path, ".git");
            if (!Directory.Exists(gitDir))
                throw new ArgumentException(nameof(path), ".git directory not found.");

            mObjectDir = new DirectoryInfo(Path.Combine(gitDir, "objects"));
            if (!mObjectDir.Exists)
                throw new InvalidDataException("Missing 'objects' dir in .git dir.");

            foreach (var subDir in mObjectDir.GetDirectories())
            {
                if (subDir.Name.Length == 2)
                {
                    foreach (var o in subDir.EnumerateFiles())
                    {
                        //inspectLooseObject(o);
                    }
                }
                else if (subDir.Name == "pack")
                {
                    foreach (var pack in subDir.GetFiles())
                    {
                        if (pack.Extension == ".pack")
                        {
                            mPackFiles.Add(new PackFile(pack));
                        }
                    }
                }
            }
        }

        public IEnumerable<ObjectId> EnumerateObjects()
        {
            foreach (var pack in mPackFiles)
            {
                foreach (var objId in pack.EnumerateObjects())
                {
                    yield return objId;
                }
            }
            foreach (var subDir in mObjectDir.GetDirectories().Where(d => d.Name.Length == 2))
            {
                foreach (var file in subDir.EnumerateFiles())
                {
                    yield return ObjectId.Parse(subDir.Name + file.Name);
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

        void inspectLooseObject(FileInfo objectFi)
        {
            ObjectId objId = ObjectId.Parse(objectFi.Directory.Name + objectFi.Name);

            var sha = SHA1.Create();
            string tag;
            byte[] objectContents;


            using (var fs = objectFi.OpenRead())
            {
                //TODO: apply some not-invented-here syndrom by reimplmenting inflate
                using (var inflater = new InflaterInputStream(fs))
                {
                    int? spaceNdx = null;
                    int headerBytesRead = 0;
                    byte[] headerBytes = new byte[30]; //should be enough for a 64-bit size
                    while (true)
                    {
                        int b = inflater.ReadByte();
                        if (b == -1)
                            throw new Exception("Unexpected EOF");

                        headerBytes[headerBytesRead] = (byte)b;

                        if (b == ' ')
                            spaceNdx = headerBytesRead;

                        headerBytesRead++;

                        if (b == 0)
                            break;

                        if (headerBytesRead == headerBytes.Length)
                            throw new Exception("Header too big.");
                    }
                    if (!spaceNdx.HasValue)
                        throw new Exception("Did not find space.");

                    //split the string along the space to get the object type and size and size
                    tag = Encoding.ASCII.GetString(headerBytes, 0, spaceNdx.Value);
                    string sizeStr = Encoding.ASCII.GetString(headerBytes, spaceNdx.Value + 1, headerBytesRead - spaceNdx.Value - 2);
                    int objectSize = int.Parse(sizeStr, NumberStyles.None, CultureInfo.InvariantCulture);
                    objectContents = new byte[objectSize];
                    if (inflater.Read(objectContents, 0, objectSize) != objectSize)
                        throw new Exception("Short read.");

                    sha.TransformBlock(headerBytes, 0, headerBytesRead, null, 0);
                }
            }

            sha.TransformFinalBlock(objectContents, 0, objectContents.Length);
            if (!objId.Equals(new ObjectId(sha.Hash)))
                throw new Exception("Hash is not right!");

            Console.WriteLine("{0}: {1}", tag, objectContents.Length);

            switch (tag)
            {
                case "blob":
                    break;
                case "commit":
                    inspectCommit(objectContents);
                    break;
                case "tree":
                    inspectTree(objectContents);
                    break;
                default:
                    throw new Exception("Unrecognized object type: " + tag);
            }
        }

        class PersonTime
        {
            static readonly DateTime sEpoc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
