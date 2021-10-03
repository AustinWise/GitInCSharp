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
            if (tagType != "commit" && tagType != "tag")
                throw new Exception($"Tag type is not commit. Is that ok? ObjectID: {objId} Type type: {tagType}");


            Object = ObjectId.Parse(dict["object"]);
            Tagger = PersonTime.Parse(dict["tagger"]);
            TagDescription = texty.Body;
        }

        /// <summary>
        /// May be a commit or another tag.
        /// </summary>
        public ObjectId Object { get; }
        public string TagName { get; }
        public PersonTime Tagger { get; }
        public string TagDescription { get; }
    }
}
