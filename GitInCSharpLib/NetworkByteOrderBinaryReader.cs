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
            byte b0 = ReadByte();
            byte b1 = ReadByte();
            return (short)((b0 << 8) | b1);
        }

        public int ReadInt32()
        {
            byte b0 = ReadByte();
            byte b1 = ReadByte();
            byte b2 = ReadByte();
            byte b3 = ReadByte();
            return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
        }

        public long ReadInt64()
        {
            long b0 = ReadByte();
            long b1 = ReadByte();
            long b2 = ReadByte();
            long b3 = ReadByte();
            long b4 = ReadByte();
            long b5 = ReadByte();
            long b6 = ReadByte();
            long b7 = ReadByte();
            return (b0 << 56) | (b1 << 48) | (b2 << 40) | (b3 << 32) | (b4 << 24) | (b5 << 16) | (b6 << 8) | b7;
        }

        public ushort ReadUInt16()
        {
            byte b0 = ReadByte();
            byte b1 = ReadByte();
            return (ushort)((b0 << 8) | b1);
        }

        public uint ReadUInt32()
        {
            byte b0 = ReadByte();
            byte b1 = ReadByte();
            byte b2 = ReadByte();
            byte b3 = ReadByte();
            return (uint)((b0 << 24) | (b1 << 16) | (b2 << 8) | b3);
        }

        public ulong ReadUInt64()
        {
            ulong b0 = ReadByte();
            ulong b1 = ReadByte();
            ulong b2 = ReadByte();
            ulong b3 = ReadByte();
            ulong b4 = ReadByte();
            ulong b5 = ReadByte();
            ulong b6 = ReadByte();
            ulong b7 = ReadByte();
            return (b0 << 56) | (b1 << 48) | (b2 << 40) | (b3 << 32) | (b4 << 24) | (b5 << 16) | (b6 << 8) | b7;
        }
    }
}
