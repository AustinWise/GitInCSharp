using Austin.GitInCSharpLib;
using System;

namespace GitInCSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            int i = 0;
            // var repo = new Repo(Environment.CurrentDirectory);
            using var repo = new Repo(@"e:\externsrc\git");
            foreach (var objId in repo.EnumerateObjects())
            {
                var info = repo.ReadObject(objId);
                // Console.WriteLine("{0}: {1}", objId.IdStr, info.ID);
                // Console.Write('.');
                i++;
                if (i == 1024)
                {
                    i = 0;
                    Console.Write('+');
                }
            }
        }
    }
}
