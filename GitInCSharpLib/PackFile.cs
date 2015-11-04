using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Austin.GitInCSharpLib
{
    class PackFile
    {
        readonly int[] mFanOut;
        readonly ObjectId[] mObjectIds;
        readonly uint[] mOffsets;
        readonly long[] mBigOffsets;
        readonly FileStream mPackFile;

        public PackFile(FileInfo packFi)
        {
            string idxPath = packFi.FullName.Substring(0, packFi.FullName.Length - packFi.Extension.Length);
            idxPath += ".idx";
            var idxFi = new FileInfo(idxPath);
            if (!idxFi.Exists)
                throw new FileNotFoundException("Could not find index.");

            ObjectId packfileHash = ObjectId.Parse(packFi.Name.Substring(5, 40)); //pack-{hash}

            int numberOfObjects;

            using (var fs = packFi.OpenRead())
            {
                if (fs.ReadByte() != 'P')
                    throw new Exception("Bad header");
                if (fs.ReadByte() != 'A')
                    throw new Exception("Bad header");
                if (fs.ReadByte() != 'C')
                    throw new Exception("Bad header");
                if (fs.ReadByte() != 'K')
                    throw new Exception("Bad header");

                var br = new NetworkByteOrderBinaryReader(fs);

                if (br.ReadInt32() != 2)
                    throw new Exception("Bad pack version.");

                numberOfObjects = br.ReadInt32();

                if (numberOfObjects < 0)
                {
                    //this assumption makes things a lot easier, like we can use the built-in binary search function.
                    throw new NotImplementedException("Support for more then 2^31 obects in a pack file is not implemented.");
                }

                //read the hash from the end
                fs.Seek(-20, SeekOrigin.End);
                if (!packfileHash.Equals(ObjectId.ReadFromStream(fs)))
                    throw new Exception("Packfile stored hash does not match file name.");

                //check the hash
                fs.Seek(0, SeekOrigin.Begin);
                var computedHash = Sha1.ComputeHash(new SubsetStream(fs, fs.Length - 20));
                if (!computedHash.Equals(packfileHash))
                    throw new Exception("Hash of pack contents does match stored contents or filename.");

                fs.Seek(12, SeekOrigin.Begin);
                while (fs.Position != fs.Length)
                {
                    byte b = br.ReadByte();
                    var type = (PackObjectType)((b >> 4) & 0x7);
                    int uncompressedSize = b & 0xf;
                    int shift = 4;
                    while ((b & 0x80) == 0x80)
                    {
                        if (shift >= 25)
                            throw new Exception("Object size does nto fit in a 32-bit integer.");
                        b = br.ReadByte();
                        uncompressedSize |= ((b & 0x7f) << shift);
                        shift += 7;
                    }

                    long before = fs.Position;

                    byte[] decompressedObject = new byte[uncompressedSize];
                    using (var inflator = new InflaterInputStream(fs))
                    {
                        var bytesRead = inflator.Read(decompressedObject, 0, uncompressedSize);
                        if (uncompressedSize != bytesRead)
                            throw new Exception("Short read.");
                    }

                    //SharpZipLib reads more bytes from the source stream than it really needs,
                    //so we have to stop after the first object for now.
                    //Addtionally, it closes the base stream when closed itself.
                    break;
                }
            }

            mFanOut = new int[256];
            mObjectIds = new ObjectId[numberOfObjects];
            mOffsets = new uint[numberOfObjects];

            using (var fs = idxFi.OpenRead())
            {
                //check the pack checksum
                fs.Seek(-40, SeekOrigin.End);
                if (ObjectId.ReadFromStream(fs) != packfileHash)
                    throw new Exception("Index's copy of the pack file hash does not match.");
                ObjectId indexHash = ObjectId.ReadFromStream(fs);

                //check the index file checksum
                fs.Seek(0, SeekOrigin.Begin);
                if (indexHash != Sha1.ComputeHash(new SubsetStream(fs, fs.Length - 20)))
                    throw new Exception("Checksum of index file is not correct.");

                //load all the data now
                fs.Seek(0, SeekOrigin.Begin);


                //check header \377tOc
                if (fs.ReadByte() != 0xff)
                    throw new Exception("Bad header");
                if (fs.ReadByte() != 't')
                    throw new Exception("Bad header");
                if (fs.ReadByte() != 'O')
                    throw new Exception("Bad header");
                if (fs.ReadByte() != 'c')
                    throw new Exception("Bad header");


                var br = new NetworkByteOrderBinaryReader(fs);
                int version = br.ReadInt32();

                if (version != 2)
                    throw new Exception("Unexpected pack index version: " + version);


                for (int i = 0; i < mFanOut.Length; i++)
                {
                    mFanOut[i] = br.ReadInt32();
                }

                for (int i = 0; i < numberOfObjects; i++)
                {
                    mObjectIds[i] = ObjectId.ReadFromStream(fs);
                    //if (i != 0 && mObjectIds[i-1].c)
                }

                //skip past 
                fs.Seek((long)numberOfObjects * 4, SeekOrigin.Current);

                int bigFileCount = 0;
                for (int i = 0; i < numberOfObjects; i++)
                {
                    var oft = br.ReadUInt32();
                    mOffsets[i] = oft;
                    if ((oft & 0x80000000) != 0)
                        bigFileCount++;
                }

                mBigOffsets = new long[bigFileCount];
                for (int i = 0; i < bigFileCount; i++)
                {
                    Debug.Assert(false, "this is as of yet untested, so test it and remove this assert");
                    mBigOffsets[i] = br.ReadInt64();
                }
            }

            mPackFile = packFi.OpenRead();
        }

        public IEnumerable<ObjectId> EnumerateObjects()
        {
            //TODO: perhaps try a little hard to not give out a mutable reference to our array
            return mObjectIds;
        }

        public bool ContainsObject(ObjectId objId)
        {
            return TryGetOffset(objId).HasValue;
        }

        long? TryGetOffset(ObjectId objId)
        {
            int lowerBound = mFanOut[objId.FirstByte];
            int upperBound;
            if (objId.FirstByte == 0xff)
                upperBound = mObjectIds.Length;
            else
                upperBound = mFanOut[objId.FirstByte + 1];

            int offset = Array.BinarySearch<ObjectId>(mObjectIds, lowerBound, upperBound - lowerBound, objId);
            if (offset < 0)
            {
                return null;
            }

            uint packOffset = mOffsets[offset];
            if ((packOffset & 0x80000000) != 0)
            {
                return mBigOffsets[packOffset & 0x7fffffff];
            }
            else
            {
                return packOffset;
            }

            throw new NotImplementedException();
        }
    }
}
