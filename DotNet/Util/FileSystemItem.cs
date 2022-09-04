namespace ADLib.Util;

public class FileSystemItem
{
    public readonly string OriginalPath;

    public string Filename => Path.GetFileName(OriginalPath);

    public FileSystemItem(string originalPath)
    {
        OriginalPath = originalPath;
    }

    public string GetContainingDirectory()
    {
        var dir = Path.GetDirectoryName(OriginalPath);
        if (dir == null)
        {
            throw new Exception($"Unexpected root level path: {OriginalPath}");
        }

        return dir;
    }
}