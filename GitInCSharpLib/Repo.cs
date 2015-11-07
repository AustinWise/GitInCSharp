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

        internal Tuple<PackObjectType, byte[]> ReadRawObject(ObjectId objId)
        {
            foreach (var pack in mPackFiles)
            {
                if (pack.ContainsObject(objId))
                    return pack.ReadObject(objId);
            }
            var looseFi = new FileInfo(Path.Combine(mObjectDir.FullName, objId.LooseFileName));
            if (looseFi.Exists)
                return readLooseObject(looseFi);
            throw new Exception("Could not find object: " + objId.IdStr);
        }

        public GitObject ReadObject(ObjectId objId)
        {
            var contents = ReadRawObject(objId);
            switch (contents.Item1)
            {
                case PackObjectType.Commit:
                    return new Commit(this, objId, contents.Item2);
                case PackObjectType.Tree:
                    return new Tree(this, objId, contents.Item2);
                case PackObjectType.Blob:
                    return new Blob(this, objId, contents.Item2);
                case PackObjectType.Tag:
                    return new AnnotedTag(this, objId, contents.Item2);

                default:
                    throw new NotSupportedException("Unsupport object type: " + contents.Item1);
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

        Tuple<PackObjectType, byte[]> readLooseObject(FileInfo objectFi)
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

            PackObjectType objType;
            switch (tag)
            {
                case "blob":
                    objType = PackObjectType.Blob;
                    break;
                case "commit":
                    objType = PackObjectType.Commit;
                    break;
                case "tree":
                    objType = PackObjectType.Tree;
                    break;
                case "tag":
                    objType = PackObjectType.Tag;
                    break;
                default:
                    throw new Exception("Unrecognized object type: " + tag);
            }

            return new Tuple<PackObjectType, byte[]>(objType, objectContents);
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
