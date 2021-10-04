using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Austin.GitInCSharpLib
{
    class NetworkByteOrderBinaryReader
    {
        readonly Stream mStream;

        public NetworkByteOrderBinaryReader(Stream s)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));
            this.mStream = s;
        }

        public byte ReadByte()
        {
            int ret = mStream.ReadByte();
            if (ret == -1)
                throw new EndOfStreamException();
            return (byte)ret;
        }

        public short ReadInt16()
        {
            Span<byte> b = stackalloc byte[2];
            int read = mStream.Read(b);
            if (read != 2)
                throw new EndOfStreamException();
            return (short)(b[0] << 8 | b[1]);
        }

        public uint ReadUInt32()
        {
            Span<byte> b = stackalloc byte[4];
            int read = mStream.Read(b);
            if (read != 4)
                throw new EndOfStreamException();
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | (uint)b[3];
        }

        public ulong ReadUInt64()
        {
            Span<byte> b = stackalloc byte[8];
            int read = mStream.Read(b);
            if (read != 8)
                throw new EndOfStreamException();
            return ((ulong)b[0] << 56) | ((ulong)b[1] << 48) | ((ulong)b[2] << 40) | ((ulong)b[3] << 32) | ((ulong)b[4] << 24) | ((ulong)b[5] << 16) | ((ulong)b[6] << 8) | (ulong)b[7];
        }

        public ushort ReadUInt16()
        {
            return unchecked((ushort)ReadInt16());
        }

        public int ReadInt32()
        {
            return unchecked((int)ReadUInt32());
        }

        public long ReadInt64()
        {
            return unchecked((long)ReadUInt64());
        }
    }
}
