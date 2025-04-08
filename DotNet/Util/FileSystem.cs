using ADLib.Exceptions;
using ADLib.Logging;
using Microsoft.VisualBasic.FileIO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SearchOption = System.IO.SearchOption;

namespace ADLib.Util;

public enum OverwriteMode
{
    Throw,

    Overwrite,

    RenameIfDifferent,

    Abort
}

public static class FileSystem
{
    public static void CreateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            return;
        }

        GenLog.Info($"Creating directory {path}");
        Directory.CreateDirectory(path);
    }

    // Create or clean out given directory
    public static void InitialiseDirectory(string path)
    {
        DeleteDirectory(path);
        CreateDirectory(path);
    }

    public static async Task InitialiseDirectoryAsync(string path)
    {
        await DeleteAsync(path).ConfigureAwait(false);
        CreateDirectory(path);
    }

    public static void CopyFile(string src, string dst, bool force = false)
    {
        GenLog.Info($"Copying {src} to {dst}");
        if (!File.Exists(src))
        {
            throw new FileNotFoundException("src");
        }

        if (Directory.Exists(dst))
        {
            var fileName = Path.GetFileName(src);
            if (fileName == null)
            {
                throw new ConfigurationException($"Cannot determine filename from {src}");
            }

            dst = Path.Combine(dst, fileName);
        }

        File.Copy(src, dst, force);
    }

    public static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            DeleteDirectory(path);
            return;
        }

        if (!File.Exists(path))
        {
            return;
        }

        Retry.OnException(() => File.Delete(path), $"Deleting {path}");
    }

    public static async Task DeleteAsync(string path)
    {
        if (Directory.Exists(path))
        {
            await Task.Run(() => DeleteDirectory(path)).ConfigureAwait(false);
            return;
        }

        if (!File.Exists(path))
        {
            return;
        }

        await Retry.OnExceptionAsync(
                async () => await Task.Run(() => File.Delete(path)).ConfigureAwait(false),
                $"Deleting {path}",
                CancellationToken.None)
            .ConfigureAwait(false);

        await EnsurePathGoneAsync(path).ConfigureAwait(false);
    }

    public static async Task WriteToFileSafelyAsync(string path, string[] content)
    {
        async Task WriteToFile()
        {
            await File.WriteAllLinesAsync(path, content).ConfigureAwait(false);
        }

        GenLog.Debug($"Writing to {path}");
        await Retry.OnExceptionAsync(WriteToFile, null, 5).ConfigureAwait(false);
    }

    public static async Task WriteToFileSafelyAsync(string path, string content)
    {
        async Task WriteToFile()
        {
            await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
        }

        GenLog.Debug($"Writing to {path}");
        await Retry.OnExceptionAsync(WriteToFile, null, 5).ConfigureAwait(false);
    }

    public static void CopyWithoutMirror(string source, string destination)
    {
        DoRobocopy(source, destination, "/e", 3);
    }

    public static void CopyWithMirror(string source, string destination)
    {
        DoRobocopy(source, destination, "/MIR", 4);
    }

    public static string? SearchUpForFileFrom(
        string filename,
        string startingLocation,
        string? underSubDir = null)
    {
        var result = TrySearchUpForFileFrom(out var foundPath, filename, startingLocation, underSubDir);

        if (!result)
        {
            throw new ConfigurationException(
                $"Data file {filename} not found searching up from {startingLocation}");
        }

        return foundPath;
    }

    public static bool TrySearchUpForFileFrom(
        out string? foundPath,
        string filename,
        string startingLocation,
        string? underSubDir = null)
    {
        var directoryToCheck = startingLocation;
        GenLog.Info($"Searching for {filename} starting from {startingLocation}");

        while (directoryToCheck != null)
        {
            var possibleFileLocation = !string.IsNullOrWhiteSpace(underSubDir)
                ? Path.Combine(directoryToCheck, underSubDir, filename)
                : Path.Combine(directoryToCheck, filename);

            if (File.Exists(possibleFileLocation) || Directory.Exists(possibleFileLocation))
            {
                GenLog.Info($"Found at {possibleFileLocation}");
                foundPath = possibleFileLocation;
                return true;
            }

            directoryToCheck = Directory.GetParent(directoryToCheck)?.FullName;
        }

        foundPath = null;
        return false;
    }

    public static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new ConfigurationException($"Expected file not found: {path}");
        }
    }

    public static string GetZInternalsDir()
    {
        return Path.Combine(Path.GetTempPath(), "ZInternals");
    }

    // Tests for file or directory
    public static bool Exists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    public static string MoveFileToDir(string file, string dir, bool makeUnique = false)
    {
        if (!File.Exists(file))
        {
            throw new InvalidOperationException($"Cannot move non-existent file: {file}");
        }

        var sourceDir = Path.GetDirectoryName(file);
        if (sourceDir!.EqualsIgnoringCase(dir))
        {
            throw new InvalidOperationException($"Cannot move file to same directory: {file}");
        }

        CreateDirectory(dir);
        var fileName = Path.GetFileName(file);
        var dest = makeUnique ? GetUniquelyNamedFileIn(dir, fileName) : Path.Combine(dir, fileName);

        if (File.Exists(dest))
        {
            throw new InvalidAssumptionException($"Cannot move '{file}' to '{dest}'. Target already exists.");
        }

        Retry.OnException(() => File.Move(file, dest), $"Moving '{file}' to dir '{dir}'");
        return dest;
    }

    public static async Task<(bool success, string newFile)> MoveFileToDirAsync(
        string file,
        string targetDir,
        OverwriteMode overwriteMode)
    {
        return await Task.Run(() => MoveFileToDir(file, targetDir, overwriteMode)).ConfigureAwait(false);
    }

    public static (bool success, string newFile) MoveFileToDir(
        string file,
        string targetDir,
        OverwriteMode overwriteMode)
    {
        if (!File.Exists(file))
        {
            throw new FileNotFoundException($"Cannot move non-existent file: {file}");
        }

        CreateDirectory(targetDir);
        var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
        if (File.Exists(targetFile))
        {
            switch (overwriteMode)
            {
                case OverwriteMode.Throw:
                    throw new InvalidOperationException($"File already exists: {targetFile}");

                case OverwriteMode.RenameIfDifferent when AreFilesTheSame(file, targetFile):
                    GenLog.Info($"Identical files {file} == {targetFile}, removing source");
                    DeleteFileToRecycleBin(file);
                    return (true, targetFile);

                case OverwriteMode.RenameIfDifferent:
                    GenLog.Info($"File exists in both places and are different: {Path.GetFileName(file)}");
                    targetFile = GetUniquelyNamedFileIn(targetDir, Path.GetFileName(file));
                    GenLog.Info($"Renaming to {targetFile}");
                    break;

                case OverwriteMode.Abort:
                    GenLog.Info($"File exists, will abort: {targetFile}");
                    return (false, "");
            }
        }

        if (File.Exists(targetFile))
        {
            DeleteFileToRecycleBin(targetFile);
        }

        Retry.OnException(() => File.Move(file, targetFile), $"Moving '{file}' to dir '{targetDir}'");
        return (true, targetFile);
    }

    public static async Task<string> MoveFileToDirAsync(string file, string dir, bool makeUnique = false)
    {
        return await Task.Run(() => MoveFileToDir(file, dir, makeUnique)).ConfigureAwait(false);
    }

    public static void MoveDirectoryContents(
        string sourceDir,
        string targetDir,
        OverwriteMode overwriteMode,
        bool deleteEmptySourceDir)
    {
        GenLog.Info($"Moving contents of {sourceDir} to {targetDir}");
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            MoveFileToDir(file, targetDir, overwriteMode);
        }

        foreach (var subDirectory in Directory.GetDirectories(sourceDir))
        {
            var targetDirectory = Path.Combine(targetDir, Path.GetFileName(subDirectory));
            MoveDirectoryContents(subDirectory, targetDirectory, overwriteMode, deleteEmptySourceDir);
        }

        if (deleteEmptySourceDir)
        {
            Directory.Delete(sourceDir);
        }
    }

    public static Task MoveDirectoryContentsAsync(
        string sourceDir,
        string targetDir,
        OverwriteMode overwriteMode,
        bool deleteEmptySourceDir)
    {
        return Task.Run(
            () => MoveDirectoryContents(sourceDir, targetDir, overwriteMode, deleteEmptySourceDir));
    }

    // Removes illegal chars from filename
    public static string GetCleanFilename(string path, bool asciiOnly = false)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            path = path.Replace(invalidChar, '_');
        }

        var result = path.Trim(' ').TrimEnd('.', ' ');
        return asciiOnly ? StringUtils.GetWithoutNonASCIIChars(result) : result;
    }

    public static void DeleteFileToRecycleBin(string? path)
    {
        DeleteFileToRecycleBinAsync(path, CancellationToken.None).Wait();
    }

    public static async Task DeleteFileToRecycleBinAsync(
        string? path,
        CancellationToken cancellationToken = default)
    {
        if (path!.IsEmpty())
        {
            return;
        }

        if (Directory.Exists(path))
        {
            throw new InvalidAssumptionException($"Asked to delete file, but is a directory: {path}");
        }

        if (!File.Exists(path))
        {
            return;
        }

        await Retry.OnExceptionAsync(
                async () => await Task.Run(
                        () => Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            path,
                            UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin),
                        cancellationToken)
                    .ConfigureAwait(false),
                $"Deleting file to recycle bin: {path}",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task DeleteDirToRecycleBinAsync(
        string? path,
        CancellationToken cancellationToken = default)
    {
        if (path.IsEmpty())
        {
            return;
        }

        if (File.Exists(path))
        {
            throw new InvalidAssumptionException($"Asked to delete directory, but is a file: {path}");
        }

        if (!Directory.Exists(path))
        {
            return;
        }

        await Retry.OnExceptionAsync(
                async () => await Task.Run(
                        () => Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            path,
                            UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin),
                        cancellationToken)
                    .ConfigureAwait(false),
                $"Deleting directory to recycle bin: {path}",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<string[]> GetFileEntriesUnderFastAsync(string dir, bool recurse = false)
    {
        var allEntries = await GetAllFilesystemEntriesUnderFastAsync(dir, recurse).ConfigureAwait(false);
        return allEntries
            .Where(File.Exists)
            .ToArray();
    }

    public static async Task<string[]> GetDirectoriesUnderFastAsync(string dir, bool recurse = false)
    {
        // TODO: Use dir output parsing to determine files vs directories
        var allEntries = await GetAllFilesystemEntriesUnderFastAsync(dir, recurse).ConfigureAwait(false);
        return allEntries
            .Where(Directory.Exists)
            .ToArray();
    }

    public static async Task<string[]> GetDirectoriesUnderAsync(string root)
    {
        return await Task.Run(() => Directory.EnumerateDirectories(root).ToArray()).ConfigureAwait(false);
    }

    public static async Task<string[]> GetFilesUnderAsync(string root, bool recurse)
    {
        return await GetFilesUnderAsync(root, recurse, CancellationToken.None).ConfigureAwait(false);
    }

    public static async Task<string[]> GetFilesUnderAsync(
        string root,
        bool recurse,
        CancellationToken stoppingToken)
    {
        if (!Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        GenLog.Debug($"Finding all files under {root}");
        return await Task.Run(
                () => Directory.GetFiles(
                    root,
                    "*.*",
                    recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly),
                stoppingToken)
            .ConfigureAwait(false);
    }

    public static string GetUniquelyNamedFileIn(string dir, string baseFilename)
    {
        if (!Directory.Exists(dir))
        {
            throw new InvalidOperationException($"Directory does not exist: {dir}");
        }

        var basename = Path.GetFileNameWithoutExtension(baseFilename);
        var extension = Path.GetExtension(baseFilename);
        var index = 0;
        while (true)
        {
            var suffix = index == 0 ? "" : $" {index}";
            var remoteFilename = $"{basename}{suffix}{extension}";
            var remotePath = Path.Combine(dir, remoteFilename);
            if (!File.Exists(remotePath))
            {
                return remotePath;
            }

            index++;
        }
    }

    // Handle long paths on Windows
    public static string GetFixedWindowsPath(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !path.StartsWith(@"\\?\"))
        {
            // Handle long paths
            return @"\\?\" + path;
        }

        return path;
    }

    public static async Task<bool> IsDirEmptyOfFilesAsync(string dir)
    {
        return (await GetFileEntriesUnderFastAsync(dir, true).ConfigureAwait(false)).Length == 0;
    }

    // Note: Unicode filenames will be mangled, does not handle special chars in path
    private static async Task<IEnumerable<string>> GetAllFilesystemEntriesUnderFastAsync(
        string dir,
        bool recurse)
    {
        if (!Directory.Exists(dir))
        {
            throw new DirectoryNotFoundException($"Directory not found: {dir}");
        }

        if (dir.Contains(@"&"))
        {
            GenLog.Debug($"Directory contains '&' character, will revert to slow mode: {dir}");
            // Use C# built-in method to return all filesystem entries under `dir`
            return recurse
                ? Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories)
                : Directory.EnumerateFileSystemEntries(dir);
        }

        var parameters = new List<object> { "/c", "dir", "/b", dir };

        if (recurse)
        {
            parameters.Add("/s");
        }

        var allFilesRaw = await Shell.RunSilentAndFailIfNotExitZeroAsync("cmd.exe", parameters.ToArray())
            .ConfigureAwait(false);

        if (allFilesRaw.IsEmpty())
        {
            return [];
        }

        return allFilesRaw.Split(Environment.NewLine)
            .Where(f => f.IsNotEmpty())
            .Select(f => recurse ? f : Path.Combine(dir, f));
    }

    private static bool AreFilesTheSame(string file1, string file2)
    {
        var file1Info = new FileInfo(file1);
        var file2Info = new FileInfo(file2);
        if (file1Info.Length != file2Info.Length)
        {
            return false;
        }

        using var md5 = MD5.Create();
        using var stream1 = File.OpenRead(file1);
        using var stream2 = File.OpenRead(file2);
        var checksum1 = md5.ComputeHash(stream1);
        var checksum2 = md5.ComputeHash(stream2);

        return BitConverter.ToString(checksum1) == BitConverter.ToString(checksum2);
    }

    private static async Task EnsurePathGoneAsync(string path)
    {
        var retries = 20;
        while (retries-- > 0)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        throw new IOException($"Unable to delete file {path}");
    }

    private static void DeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var retries = 10;
        while (Directory.Exists(path))
        {
            try
            {
                GenLog.Info($"Deleting directory: {path}...");
                Directory.Delete(path, true);
            }
            catch (IOException e) when (retries-- >= 0)
            {
                GenLog.Warning("Unable to delete directory. Will retry...");
                GenLog.Warning(e.Message);
                Thread.Sleep(5000);
            }
            catch (UnauthorizedAccessException) when (retries-- >= 0)
            {
                GenLog.Warning("Unable to delete directory. Will attempt to remove read-only files...");
                RemoveReadOnlyAttributes(path);
            }
        }
    }

    private static void RemoveReadOnlyAttributes(string path)
    {
        try
        {
            foreach (var s in Directory.GetDirectories(path))
            {
                RemoveReadOnlyAttributes(s);
            }

            foreach (var f in Directory.GetFiles(path))
            {
                var attr = File.GetAttributes(f);
                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(f, attr ^ FileAttributes.ReadOnly);
                }
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void DoRobocopy(string source, string destination, string type, int exitCodeLimit)
    {
        GenLog.Info($"Robocopy ({type}) '{source}' --> '{destination}'");

        if (!Directory.Exists(source))
        {
            throw new ArgumentException($"{source} not found");
        }

        Directory.CreateDirectory(destination);

        var result = Shell.RunAndGetExitCode("robocopy", source, destination, type, "/MT", "/R:3");

        if (result > exitCodeLimit)
        {
            throw new IOException($"Robocopy returned code {result}");
        }
    }
}