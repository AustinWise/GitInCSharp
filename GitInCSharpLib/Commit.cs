using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Austin.GitInCSharpLib
{
    public sealed class Commit : GitObject
    {
        internal Commit(Repo repo, ObjectId objId, byte[] objectContents)
            : base(repo, objId)
        {
            var texty = TextyObject.Parse(objectContents);
            var parents = new List<ObjectId>();
            foreach (var kvp in texty.Attributes)
            {
                switch (kvp.Key)
                {
                    case "tree":
                        if (!Tree.IsZero)
                            throw new Exception("Too many trees!");
                        Tree = ObjectId.Parse(kvp.Value);
                        break;
                    case "parent":
                        parents.Add(ObjectId.Parse(kvp.Value));
                        break;
                    case "author":
                        Author = PersonTime.Parse(kvp.Value);
                        break;
                    case "committer":
                        Committer = PersonTime.Parse(kvp.Value);
                        break;
                    case "gpgsig":
                    case "mergetag":
                        //TODO: unparsed headers
                        break;
                    default:
                        Console.WriteLine(System.Text.Encoding.UTF8.GetString(objectContents));
                        Console.WriteLine();
                        throw new Exception("Unexpected commit attribute2: " + kvp.Key + Environment.NewLine + kvp.Value);
                }
            }
        }

        public ObjectId Tree { get; }
        public ReadOnlyCollection<ObjectId> Parents { get; }
        public PersonTime Author { get; }
        public PersonTime Committer { get; }
    }
}
