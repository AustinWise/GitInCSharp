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
    unsafe struct ObjectId
    {
        public const int SIZE = 20;

        [FieldOffset(0)]
        public fixed byte Bytes[SIZE];

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(SIZE * 2);
            fixed   (byte* bPtr  = this.Bytes)
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

        public static ObjectId ReadFromStream(Stream s)
        {
            var ret = new ObjectId();
            for (int i = 0; i < SIZE; i++)
            {
                int b = s.ReadByte();
                if (b == -1)
                    throw new Exception("Unexpected new line.");
                ret.Bytes[i] = (byte)b;
            }
            return ret;
        }
    }
}
