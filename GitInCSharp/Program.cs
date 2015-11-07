using Austin.GitInCSharpLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitInCSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            var repo = new Repo(Environment.CurrentDirectory);
            var repo = new Repo(@"c:\src\austinwise.github.com\");
            foreach (var objId in repo.EnumerateObjects())
            {
                var info = repo.ReadObject(objId);
                Console.WriteLine("{0}: {1}", objId.IdStr, info.ID);
            }
        }
    }
}
