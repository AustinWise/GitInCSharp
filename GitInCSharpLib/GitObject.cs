using System;

namespace Austin.GitInCSharpLib
{
    public abstract class GitObject
    {
        protected readonly Repo mRepo;

        protected GitObject(Repo repo, ObjectId objId)
        {
            if (repo == null)
                throw new ArgumentNullException(nameof(repo));

            this.mRepo = repo;
            this.ID = objId;
        }

        public ObjectId ID { get; }
    }
}
