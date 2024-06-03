namespace ADLib.Util;

public class FileSystemItem
{
    public FileSystemItem(string originalPath)
    {
        OriginalPath = originalPath;
    }

    public string Filename => Path.GetFileName(OriginalPath);

    public string Basename => Path.GetFileNameWithoutExtension(OriginalPath);

    public string Extension => Path.GetExtension(OriginalPath);

    public string? Directory => Path.GetDirectoryName(OriginalPath);

    public readonly string OriginalPath;

    public string GetNonRootContainingDirectory()
    {
        if (Directory == null)
        {
            throw new Exception($"Unexpected root level path: {OriginalPath}");
        }

        return Directory;
    }

    public bool IsInDirectory(string dir)
    {
        return GetNonRootContainingDirectory().EqualsIgnoringCase(dir);
    }

    public override string ToString()
    {
        return OriginalPath;
    }
}