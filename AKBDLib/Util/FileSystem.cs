using AKBDLib.Logging;
using System;
using System.IO;
using System.Threading;

namespace AKBDLib.Util
{
    public static class FileSystem
    {
        public static void DeleteDirectory(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                return;

            var retries = 5;
            while (Directory.Exists(path))
            {
                try
                {
                    GenLog.Info($"Deleting directory: {path}...");
                    Directory.Delete(path, true);

                }
                catch (Exception e) when (e is IOException && retries-- >= 0)
                {
                    GenLog.Warning("Unable to delete directory. Will retry...");
                    Thread.Sleep(3000);
                }
                catch (UnauthorizedAccessException) when (retries-- >= 0)
                {
                    GenLog.Warning("Unable to delete directory. Will attempt to remove read-only files...");
                    DeleteReadOnlyDirectory(path);
                }
            }
        }

        public static void Copy(string src, string dest)
        {
            GenLog.Info($"Copying {src} to {dest}");
            if (!File.Exists(src))
            {
                throw new FileNotFoundException("src");
            }

            File.Copy(src, dest);
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

        private static void DeleteReadOnlyDirectory(string path)
        {
            var retries = 5;

            while (Directory.Exists(path))
            {
                try
                {
                    foreach (var s in Directory.GetDirectories(path))
                    {
                        DeleteReadOnlyDirectory(s);
                    }

                    foreach (var f in Directory.GetFiles(path))
                    {
                        while (File.Exists(f))
                        {
                            try
                            {
                                var attr = File.GetAttributes(f);
                                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                {
                                    File.SetAttributes(f, attr ^ FileAttributes.ReadOnly);
                                }

                                File.Delete(f);
                                break;
                            }
                            catch (IOException) when (retries-- >= 0)
                            {
                                GenLog.Warning($"Unable to delete {path}. Will retry...");
                                Thread.Sleep(1000);
                            }

                        }
                    }

                    Directory.Delete(path, true);
                    break;

                }
                catch (IOException) when (retries-- >= 0)
                {
                    GenLog.Warning($"Unable to delete {path}. Will retry...");
                    Thread.Sleep(2000);
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

            var result = Shell.RunAndGetExitCode(
                "robocopy", source, destination, "/e", "/MT", "/R:3");

            if (result > 3)
            {
                throw new IOException($"Robocopy returned code {result}");
            }
        }
    }
}
