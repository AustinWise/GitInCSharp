using System;
using System.Linq;

namespace Austin.GitInCSharpLib
{
    public sealed class AnnotedTag : GitObject
    {
        internal AnnotedTag(Repo repo, ObjectId objId, byte[] objectContents)
            : base(repo, objId)
        {
            var texty = TextyObject.Parse(objectContents);
            var dict = texty.Attributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            string tagType = dict["type"];
            if (tagType != "commit")
                throw new Exception("Tag type is not commit. Is that ok?");


            Tree = ObjectId.Parse(dict["object"]);
            Tagger = PersonTime.Parse(dict["tagger"]);
            TagDescription = texty.Body;
        }

        public ObjectId Tree { get; }
        public string TagName { get; }
        public PersonTime Tagger { get; }
        public string TagDescription { get; }
    }
}
