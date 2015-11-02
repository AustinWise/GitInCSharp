using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Austin.GitInCSharpLib
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    unsafe struct ObjectId : IEquatable<ObjectId>
    {
        const int SIZE = 20;

        //TODO: investigate if using bytes, ints, or longs is fastest for various operations
        [FieldOffset(0)]
        fixed byte Bytes[SIZE];

        [FieldOffset(0)]
        int I0;
        [FieldOffset(4)]
        int I1;
        [FieldOffset(8)]
        int I2;
        [FieldOffset(12)]
        int I3;
        [FieldOffset(16)]
        int I4;

        [FieldOffset(0)]
        long L0;
        [FieldOffset(8)]
        long L1;


        public ObjectId(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != SIZE)
                throw new ArgumentOutOfRangeException(nameof(bytes), "Expected " + SIZE + " bytes.");

            //make the compiler happy
            L0 = L1 = I0 = I1 = I2 = I3 = I4 = 0;

            fixed (byte* ptr = this.Bytes)
            {
                for (int i = 0; i < SIZE; i++)
                {
                    ptr[i] = bytes[i];
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(SIZE * 2);
            fixed (byte* bPtr = this.Bytes)
            {
                for (int i = 0; i < SIZE; i++)
                {
                    byte b = bPtr[i];
                    sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }
            }
            return sb.ToString();
        }

        public string IdStr
        {
            get { return ToString(); }
        }

        public bool IsZero
        {
            get
            {
                return this.L0 == 0 && this.L1 == 0 && this.I4 == 0;
            }
        }

        public bool Equals(ObjectId other)
        {
            return this.L0 == other.L0 && this.L1 == other.L1 && this.I4 == other.I4;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            var other = (ObjectId)obj;
            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            return I0 ^ I1 ^ I2 ^ I3 ^ I4;
        }

        public static ObjectId ReadFromStream(Stream s)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            var ret = new ObjectId();
            for (int i = 0; i < SIZE; i++)
            {
                int b = s.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();
                ret.Bytes[i] = (byte)b;
            }
            return ret;
        }

        //TODO: make this not allocate a ton
        public static ObjectId Parse(string str)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            if (str.Length != SIZE * 2)
                throw new ArgumentOutOfRangeException(nameof(str), "Expected " + (SIZE * 2) + " characters.");

            var ret = new ObjectId();
            for (int i = 0; i < SIZE; i++)
            {
                ret.Bytes[i] = Convert.ToByte(str.Substring(i * 2, 2), 16);
            }
            return ret;
        }
    }
}
