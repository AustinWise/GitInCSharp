using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Austin.GitInCSharpLib
{
    public sealed class Blob : GitObject
    {
        internal Blob(Repo repo, ObjectId objId, byte[] bytes)
            : base(repo, objId)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            this.Bytes = bytes;
        }

        //TODO: ideally use some sort of immutable byte array
        public byte[] Bytes { get; }
    }
}
