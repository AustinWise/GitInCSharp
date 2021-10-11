using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Austin.GitInCSharpLib
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public unsafe struct ObjectId : IEquatable<ObjectId>, IComparable<ObjectId>
    {
        public const int SIZE = 20;

        //TODO: investigate if using bytes, ints, or longs is fastest for various operations
        [FieldOffset(0)]
        fixed byte Bytes[SIZE];

        [FieldOffset(0)]
        byte B0;

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


        public ObjectId(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != SIZE)
                throw new ArgumentOutOfRangeException(nameof(bytes), "Expected " + SIZE + " bytes.");

            //make the compiler happy
            Unsafe.SkipInit(out L0);
            Unsafe.SkipInit(out L1);
            Unsafe.SkipInit(out I0);
            Unsafe.SkipInit(out I1);
            Unsafe.SkipInit(out I2);
            Unsafe.SkipInit(out I3);
            Unsafe.SkipInit(out I4);
            Unsafe.SkipInit(out B0);

            // actually initialize contents
            fixed (byte* ptr = this.Bytes)
            {
                bytes.CopyTo(new Span<byte>(ptr, SIZE));
            }
        }

        public override string ToString()
        {
            fixed (byte* pBytes = this.Bytes)
            {
                return Convert.ToHexString(new ReadOnlySpan<byte>(pBytes, SIZE));
            }
        }

        internal byte FirstByte
        {
            get { return B0; }
        }

        public string IdStr
        {
            get { return ToString(); }
        }

        internal string LooseFileName
        {
            get
            {
                var sb = new StringBuilder(SIZE * 2 + 1);
                sb.Append(FirstByte.ToString("x2"));
                sb.Append(Path.DirectorySeparatorChar);
                fixed (byte* bPtr = Bytes)
                {
                    for (int i = 1; i < SIZE; i++)
                    {
                        sb.Append(bPtr[i].ToString("x2"));
                    }
                }
                return sb.ToString();
            }
        }

        public bool IsZero
        {
            get
            {
                return this.L0 == 0 && this.L1 == 0 && this.I4 == 0;
            }
        }

        public static bool operator ==(ObjectId a, ObjectId b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(ObjectId a, ObjectId b)
        {
            return !a.Equals(b);
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

        public unsafe int CompareTo(ObjectId other)
        {
            fixed (byte* thisBytePtr = this.Bytes)
            {
                return new ReadOnlySpan<byte>(thisBytePtr, SIZE).SequenceCompareTo(new ReadOnlySpan<byte>(other.Bytes, SIZE));
            }
        }

        public static ObjectId ReadFromStream(Stream s)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            var ret = new ObjectId();
            int read = s.Read(new Span<byte>(ret.Bytes, SIZE));
            if (read != SIZE)
                throw new EndOfStreamException();
            return ret;
        }

        public static ObjectId Parse(string str)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            if (str.Length != SIZE * 2)
                throw new ArgumentOutOfRangeException(nameof(str), "Expected " + (SIZE * 2) + " characters.");

            var ret = new ObjectId();
            for (int i = 0; i < SIZE; i++)
            {
                ret.Bytes[i] = byte.Parse(str.AsSpan().Slice(i * 2, 2), NumberStyles.HexNumber);
            }
            return ret;
        }
    }
}
