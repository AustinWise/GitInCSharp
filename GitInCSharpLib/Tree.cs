using System;
using System.Collections.Generic;
using System.Text;

namespace Austin.GitInCSharpLib
{
    public sealed class Tree : GitObject
    {
        private static int ParseOctal(ReadOnlySpan<byte> utf8Bytes)
        {
            int ret = 0;
            for (int i = 0; i < utf8Bytes.Length; i++)
            {
                byte b = utf8Bytes[i];
                if (b >= '0' && b <= '7')
                {
                    ret = ret * 8;
                    ret += b - '0';
                }
                else
                {
                    throw new Exception("Invalid octal number: " + Encoding.UTF8.GetString(utf8Bytes));
                }
            }
            return ret;
        }

        internal Tree(Repo repo, ObjectId objId, byte[] objectContents)
            : base(repo, objId)
        {
            Entries = new List<(int, string, ObjectId)>();

            var span = new ReadOnlySpan<byte>(objectContents);

            //TODO: nicer exception messages
            while (span.Length != 0)
            {
                int spaceNdx = span.IndexOf((byte)' ');
                if (spaceNdx == -1)
                    throw new Exception();
                int mode = ParseOctal(span.Slice(0, spaceNdx));

                span = span.Slice(spaceNdx + 1);
                int zeroNdx = span.IndexOf((byte)0);
                if (zeroNdx == -1)
                    throw new Exception();
                string name = Encoding.UTF8.GetString(span.Slice(0, zeroNdx));

                span = span.Slice(zeroNdx + 1);

                var objectId = new ObjectId(span.Slice(0, ObjectId.SIZE));
                span = span.Slice(ObjectId.SIZE);

                Entries.Add((mode, name, objectId));
            }
        }

        public List<(int, string, ObjectId)> Entries { get; }
    }
}
