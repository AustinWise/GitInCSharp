using System;
using System.IO;
using System.Security.Cryptography;

namespace Austin.GitInCSharpLib
{
    //TODO cache SHA1 instances or something
    static class Sha1
    {
        public static ObjectId ComputeHash(Stream s)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));
            using (var sha1 = SHA1.Create())
            {
                return new ObjectId(sha1.ComputeHash(s));
            }
        }
    }
}
