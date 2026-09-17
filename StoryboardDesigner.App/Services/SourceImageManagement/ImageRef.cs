namespace StoryboardDesigner.App.Services;

internal sealed class ImageRef
{
    public ImageRef(string scopePath, string bucket, string ownerFolderName, string channel, Func<string> getPath, Action<string> setPath)
    {
        ScopePath = scopePath;
        Bucket = bucket;
        OwnerFolderName = ownerFolderName;
        Channel = channel;
        GetPath = getPath;
        SetPath = setPath;
    }

    public string ScopePath { get; }

    public string Bucket { get; }

    public string OwnerFolderName { get; }

    public string Channel { get; }

    public Func<string> GetPath { get; }

    public Action<string> SetPath { get; }
}
