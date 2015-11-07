using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Austin.GitInCSharpLib
{
    public sealed class Tree : GitObject
    {
        internal Tree(Repo repo, ObjectId objId, byte[] objectContents)
            : base(repo, objId)
        {
        }
    }
}
