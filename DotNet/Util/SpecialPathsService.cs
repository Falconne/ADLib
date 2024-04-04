using System;
using System.Runtime.InteropServices;

namespace AlbumDownloader.Services;

public static class SpecialPathsService
{
    public static string GetDownloadsFolderPath()
    {
        SHGetKnownFolderPath(KnownFolder.Downloads, 0, IntPtr.Zero, out var downloads);
        return downloads;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out string pszPath);

    public static class KnownFolder
    {
        public static readonly Guid Downloads = new("374DE290-123F-4565-9164-39C4925E467B");
    }
}