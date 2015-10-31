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
        }
    }
}
