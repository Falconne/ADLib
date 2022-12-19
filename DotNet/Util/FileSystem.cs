using ADLib.Exceptions;
using ADLib.Logging;
using Microsoft.VisualBasic.FileIO;
using SearchOption = System.IO.SearchOption;

namespace ADLib.Util
{
    public static class FileSystem
    {
        private static void DeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

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

        public static void CreateDirectory(string path)
        {
            if (Directory.Exists(path))
                return;

            GenLog.Info($"Creating directory {path}");
            Directory.CreateDirectory(path);
        }

        // Create or clean out given directory
        public static void InitialiseDirectory(string path)
        {
            DeleteDirectory(path);
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
                    throw new ConfigurationException(
                        $"Cannot determine filename from {src}");
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
                return;

            Retry.OnException(() => File.Delete(path), $"Deleting {path}");
        }

        public static async Task DeleteAsync(string path)
        {
            if (Directory.Exists(path))
            {
                await Task.Run(() => DeleteDirectory(path));
                return;
            }

            if (!File.Exists(path))
                return;

            await Retry.OnExceptionAsync(
                async () => await Task.Run(() => File.Delete(path)),
                $"Deleting {path}",
                CancellationToken.None);
        }

        public static void WriteToFileSafely(string path, string[] content)
        {
            void WriteToFile()
            { File.WriteAllLines(path, content); }

            Retry.OnException(WriteToFile, $"Writing to {path}", 5);
        }

        public static void WriteToFileSafely(string path, string content)
        {
            void WriteToFile()
            { File.WriteAllText(path, content); }

            Retry.OnException(WriteToFile, $"Writing to {path}", 5);
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

        public static void CopyWithoutMirror(string source, string destination)
        {
            DoRobocopy(source, destination, "/e", 3);
        }

        public static void CopyWithMirror(string source, string destination)
        {
            DoRobocopy(source, destination, "/MIR", 4);
        }

        private static void DoRobocopy(string source, string destination, string type, int exitCodeLimit)
        {
            Logging.GenLog.Info(
                $"Robocopy ({type}) '{source}' --> '{destination}'");

            if (!Directory.Exists(source))
            {
                throw new ArgumentException($"{source} not found");
            }

            Directory.CreateDirectory(destination);

            var result = Shell.RunAndGetExitCode(
                "robocopy", source, destination, type, "/MT", "/R:3");

            if (result > exitCodeLimit)
            {
                throw new IOException($"Robocopy returned code {result}");
            }
        }

        public static string? SearchUpForFileFrom(
            string filename, string startingLocation, string? underSubDir = null)
        {
            var result = TrySearchUpForFileFrom(
                out var foundPath, filename, startingLocation, underSubDir);

            if (!result)
            {
                throw new ConfigurationException(
                    $"Data file {filename} not found searching up from {startingLocation}");
            }

            return foundPath;
        }

        public static bool TrySearchUpForFileFrom(
            out string? foundPath, string filename, string startingLocation, string? underSubDir = null)
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

        public static string GetWorkDir()
        {
            var filename = AppDomain.CurrentDomain.FriendlyName;
            var basename = Path.GetFileNameWithoutExtension(filename);
            var workDir = Path.Combine(GetZInternalsDir(), basename);
            GenLog.Info($"Using work dir {workDir}");
            CreateDirectory(workDir);

            return workDir;
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

        public static void UseStandardLogFile()
        {
            var name = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            GenLog.LogFile = Path.Combine(GetZInternalsDir(), $"{name}.log");
            GenLog.Info($"Writing logfile to {GenLog.LogFile}");
        }

        public static string MoveFileToDir(string file, string dir, bool makeUnique = false)
        {
            CreateDirectory(dir);
            var fileName = Path.GetFileName(file);
            var dest = makeUnique
                ? GetUniquelyNamedFileIn(dir, fileName)
                : Path.Combine(dir, fileName);

            if (File.Exists(dest))
                throw new InvalidAssumptionException($"Cannot move '{file}' to '{dest}'. Target already exists.");

            Retry.OnException(() => File.Move(file, dest), $"Moving '{file}' to dir '{dir}'");
            return dest;
        }

        public static async Task<string> MoveFileToDirAsync(string file, string dir, bool makeUnique = false)
        {
            return await Task.Run(() => MoveFileToDir(file, dir, makeUnique));
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

        public static async Task DeleteFileToRecycleBinAsync(string? path)
        {
            await DeleteFileToRecycleBinAsync(path, CancellationToken.None);
        }

        public static async Task DeleteFileToRecycleBinAsync(string? path, CancellationToken cancellationToken)
        {
            if (path!.IsEmpty())
                return;

            if (Directory.Exists(path))
                throw new InvalidAssumptionException($"Asked to delete file, but is a directory: {path}");

            if (!File.Exists(path))
                return;

            await Retry.OnExceptionAsync(async () =>
                    await Task.Run(() =>
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin), cancellationToken),
                $"Deleting file to recycle bin: {path}", cancellationToken);

        }

        public static async Task DeleteDirToRecycleBinAsync(string? path)
        {
            await DeleteDirToRecycleBinAsync(path, CancellationToken.None);
        }

        public static async Task DeleteDirToRecycleBinAsync(string? path, CancellationToken cancellationToken)
        {
            if (path.IsEmpty())
                return;

            if (File.Exists(path))
                throw new InvalidAssumptionException($"Asked to delete directory, but is a file: {path}");

            if (!Directory.Exists(path))
                return;

            await Retry.OnExceptionAsync(async () =>
                    await Task.Run(() =>
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin), cancellationToken),
                $"Deleting directory to recycle bin: {path}", cancellationToken);

        }

        // Note: Unicode filenames will be mangled, does not handle special chars like & in path
        public static async Task<string[]> GetFilesUnderFast(string dir, bool recurse = false)
        {
            if (!Directory.Exists(dir))
                throw new InvalidAssumptionException($"Directory not found: {dir}");

            var parameters = new List<object> { "/c", "dir", "/b", dir };
            if (recurse)
                parameters.Add("/s");

            var allFilesRaw = await Shell.RunSilentAndFailIfNotExitZeroAsync(
                "cmd.exe", parameters.ToArray());

            if (allFilesRaw.IsEmpty())
                return Array.Empty<string>();

            return allFilesRaw
                .Split(Environment.NewLine)
                .Where(f => f.IsNotEmpty())
                .Select(f => recurse ? f : Path.Combine(dir, f))
                .ToArray();
        }

        public static async Task<string[]> GetDirectoriesUnderAsync(string root)
        {
            return await Task.Run(() => Directory.EnumerateDirectories(root).ToArray());
        }

        public static async Task<string[]> GetFilesUnderAsync(string root, bool recurse)
        {
            return await GetFilesUnderAsync(root, recurse, CancellationToken.None);
        }

        public static async Task<string[]> GetFilesUnderAsync(string root, bool recurse, CancellationToken stoppingToken)
        {
            if (!Directory.Exists(root))
                return Array.Empty<string>();

            GenLog.Debug($"Finding all files under {root}");
            return await Task.Run(
                () => Directory.GetFiles(
                    root,
                    "*.*",
                    recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly),
                stoppingToken);
        }


        public static string GetUniquelyNamedFileIn(string dir, string baseFilename)
        {
            var basename = Path.GetFileNameWithoutExtension(baseFilename);
            var extension = Path.GetExtension(baseFilename);
            var index = 0;
            while (true)
            {
                var suffix = index == 0 ? "" : $" {index}";
                var remoteFilename = $"{basename}{suffix}{extension}";
                var remotePath = Path.Combine(dir, remoteFilename);
                if (!File.Exists(remotePath))
                    return remotePath;

                index++;
            }
        }
    }
}
