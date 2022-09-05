namespace ADLib.Util;

public class FileSystemItem
{
    public readonly string OriginalPath;

    public string Filename => Path.GetFileName(OriginalPath);

    public string? Directory => Path.GetDirectoryName(OriginalPath);

    public FileSystemItem(string originalPath)
    {
        OriginalPath = originalPath;
    }

    public string GetNonRootContainingDirectory()
    {
        if (Directory == null)
        {
            throw new Exception($"Unexpected root level path: {OriginalPath}");
        }

        return Directory;
    }
}