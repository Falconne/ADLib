using ADLib.Exceptions;
using ADLib.Logging;
using System;
using System.IO;
using System.Threading;

namespace ADLib.Util
{
    public static class FileSystem
    {
        public static void DeleteDirectory(string path)
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

        public static void Copy(string src, string dest, bool force = false)
        {
            GenLog.Info($"Copying {src} to {dest}");
            if (!File.Exists(src))
            {
                throw new FileNotFoundException("src");
            }

            if (Directory.Exists(dest))
            {
                var fileName = Path.GetFileName(src);
                if (fileName == null)
                {
                    throw new ConfigurationException(
                        $"Cannot determine filename from {src}");
                }
                dest = Path.Combine(dest, fileName);
            }
            File.Copy(src, dest, force);
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

            GenLog.Info($"Deleting {path}");
            File.Delete(path);
        }

        public static void WriteToFileSafely(string path, string[] content)
        {
            var retries = 4;
            while (true)
            {
                try
                {
                    File.WriteAllLines(path, content);
                    break;
                }
                catch (IOException) when (retries-- >= 0)
                {
                    GenLog.Warning($"Unable to write to {path}. Will retry...");
                    Thread.Sleep(3000);
                }
            }
        }

        private static void RemoveReadOnlyAttributes(string path)
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

        public static void RobocopyWithoutMirror(string source, string destination)
        {
            Logging.GenLog.Info(
                $"Robocopy without mirror '{source}' --> '{destination}'");

            if (!Directory.Exists(source))
            {
                throw new ArgumentException($"{source} not found");
            }

            Directory.CreateDirectory(destination);

            var result = Shell.RunAndGetExitCodeMS(
                "robocopy", source, destination, "/e", "/MT", "/R:3");

            if (result > 3)
            {
                throw new IOException($"Robocopy returned code {result}");
            }
        }

        public static string TrySearchUpForFileFrom(
            string filename, string startingLocation, string underSubDir = null)
        {
            var directoryToCheck = startingLocation;
            GenLog.Info($"Searching for {filename} starting from {startingLocation}");

            while (directoryToCheck != null)
            {
                var possibleFileLocation = !string.IsNullOrWhiteSpace(underSubDir)
                    ? Path.Combine(directoryToCheck, underSubDir, filename)
                    : Path.Combine(directoryToCheck, filename);

                if (File.Exists(possibleFileLocation))
                {
                    GenLog.Info($"Found at {possibleFileLocation}");
                    return possibleFileLocation;
                }

                directoryToCheck = Directory.GetParent(directoryToCheck)?.FullName;
            }

            return null;
        }

        public static void EnsureFileExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new ConfigurationException($"Expected file not found: {path}");
            }
        }
    }
}
