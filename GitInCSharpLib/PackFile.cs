using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Austin.GitInCSharpLib
{
    class PackFile : IDisposable
    {
        readonly int[] mFanOut;
        readonly ObjectId[] mObjectIds;
        readonly uint[] mOffsets;
        readonly long[] mBigOffsets;
        readonly FileStream mPackFile;

        public PackFile(FileInfo packFi)
        {
            mPackFile = packFi.OpenRead();

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

        public Tuple<PackObjectType, byte[]> ReadObject(ObjectId objId)
        {
            long? offset = TryGetOffset(objId);
            if (!offset.HasValue)
                throw new Exception("Object id not found.");

            var ret = ReadObject(offset.Value);

            //check hash
            string header = string.Format(CultureInfo.InvariantCulture, "{0} {1}\0",
                ret.Item1.ToString().ToLowerInvariant(), ret.Item2.Length);
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            using (var sha = SHA1.Create())
            {
                sha.TransformBlock(headerBytes, 0, headerBytes.Length, null, 0);
                sha.TransformFinalBlock(ret.Item2, 0, ret.Item2.Length);
                var hash = new ObjectId(sha.Hash);
                if (!hash.Equals(objId))
                    throw new Exception("Object from pack file does not have the right hash.");
            }

            return ret;
        }

        private int getDeltaHeaderSize(byte[] buf, ref int offset)
        {
            //TODO: overflow check on offset and maybe a better IndexOutOfBound message
            byte cmd;
            int i = 0;
            int size = 0;
            do
            {
                cmd = buf[offset++];
                size |= (cmd & 0x7f) << i;
                i += 7;
            } while ((cmd & 0x80) == 0x80);
            return size;
        }

        private byte[] applyDelta(byte[] basis, byte[] delta)
        {
            //TODO: maybe some range checking?

            int offset = 0;

            int basisSize = getDeltaHeaderSize(delta, ref offset);
            if (basisSize != basis.Length)
                throw new Exception("Wrong basis size.");

            int finalSize = getDeltaHeaderSize(delta, ref offset);

            byte[] ret = new byte[finalSize];
            int retOffset = 0;

            while (offset < delta.Length)
            {
                byte cmd = delta[offset++];
                if ((cmd & 0x80) == 0x80)
                {
                    int copyOffset = 0;
                    int copySize = 0;
                    if ((cmd & 0x01) != 0) copyOffset = delta[offset++];
                    if ((cmd & 0x02) != 0) copyOffset |= delta[offset++] << 8;
                    if ((cmd & 0x04) != 0) copyOffset |= delta[offset++] << 16;
                    if ((cmd & 0x08) != 0) copyOffset |= delta[offset++] << 24;
                    if ((cmd & 0x10) != 0) copySize = delta[offset++];
                    if ((cmd & 0x20) != 0) copySize |= delta[offset++] << 8;
                    if ((cmd & 0x40) != 0) copySize |= delta[offset++] << 16;

                    if (copySize == 0)
                        copySize = 0x10000;

                    //TODO: overflow check
                    Buffer.BlockCopy(basis, copyOffset, ret, retOffset, copySize);
                    retOffset += copySize;
                }
                else if (cmd != 0)
                {
                    Buffer.BlockCopy(delta, offset, ret, retOffset, cmd);
                    retOffset += cmd;
                    offset += cmd;
                }
                else
                {
                    throw new Exception("Unexpected 0 cmd.");
                }
            }

            return ret;
        }

        public Tuple<PackObjectType, byte[]> ReadObject(long offset)
        {
            long oldPosition = mPackFile.Position;

            try
            {
                var br = new NetworkByteOrderBinaryReader(mPackFile);

                mPackFile.Seek(offset, SeekOrigin.Begin);

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

                Tuple<PackObjectType, byte[]> deltaBasis = null;

                if (type == PackObjectType.OfsDelta)
                {
                    b = br.ReadByte();
                    int basisOffset = b & 0x7f;
                    while ((b & 0x80) == 0x80)
                    {
                        //TODO: check overflow
                        basisOffset += 1;
                        b = br.ReadByte();
                        basisOffset <<= 7;
                        basisOffset |= (b & 0x7f);
                    }
                    deltaBasis = ReadObject(offset - basisOffset);
                }
                else if (type == PackObjectType.RefDelta)
                {
                    var basisId = ObjectId.ReadFromStream(mPackFile);
                    deltaBasis = ReadObject(basisId);
                }
                else if (type != PackObjectType.Blob
                        && type != PackObjectType.Commit
                        && type != PackObjectType.Tree
                        && type != PackObjectType.Tag)
                {
                    throw new Exception("Unexpected object type: " + type);
                }

                byte[] decompressedObject = new byte[uncompressedSize];
                using (var inflator = new InflaterInputStream(mPackFile))
                {
                    inflator.IsStreamOwner = false;
                    var bytesRead = inflator.Read(decompressedObject, 0, uncompressedSize);
                    if (uncompressedSize != bytesRead)
                        throw new Exception("Short read.");
                }

                if (deltaBasis == null)
                    return new Tuple<PackObjectType, byte[]>(type, decompressedObject);
                else
                    return new Tuple<PackObjectType, byte[]>(deltaBasis.Item1, applyDelta(deltaBasis.Item2, decompressedObject));
            }
            finally
            {
                mPackFile.Position = oldPosition;
            }
        }

        long? TryGetOffset(ObjectId objId)
        {
            int upperBound = mFanOut[objId.FirstByte];
            int lowerBound;
            if (objId.FirstByte == 0)
                lowerBound = 0;
            else
                lowerBound = mFanOut[objId.FirstByte - 1];

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

        public void Dispose()
        {
            mPackFile.Dispose();
        }
    }
}
