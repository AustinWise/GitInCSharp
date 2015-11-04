namespace Austin.GitInCSharpLib
{
    public enum PackObjectType
    {
        Undefined = 0,

        Commit = 1,
        Tree = 2,
        Blob = 3,
        Tag = 4,
        //5 not used
        OfsDelta = 6,
        RefDelta = 7,
    }
}
