using System;
using System.IO;
using System.Threading;

namespace AKBDLib.Util
{
    public static class FileSystem
    {
        public static void DeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var retries = 5;
            while (Directory.Exists(path))
            {
                try
                {
                    Logging.Wrap.Info($"Deleting directory: {path}...");
                    Directory.Delete(path, true);

                }
                catch (Exception e) when (e is IOException && retries-- >= 0)
                {
                    Logging.Wrap.Warning("Unable to delete directory. Will retry...");
                    Thread.Sleep(3000);
                }
                catch (UnauthorizedAccessException) when (retries-- >= 0)
                {
                    Logging.Wrap.Warning("Unable to delete directory. Will attempt to remove read-only files...");
                    DeleteReadOnlyDirectory(path);
                }
            }
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
                    Logging.Wrap.Warning($"Unable to write to {path}. Will retry...");
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
                                Logging.Wrap.Warning($"Unable to delete {path}. Will retry...");
                                Thread.Sleep(1000);
                            }

                        }
                    }

                    Directory.Delete(path, true);
                    break;

                }
                catch (IOException) when (retries-- >= 0)
                {
                    Logging.Wrap.Warning($"Unable to delete {path}. Will retry...");
                    Thread.Sleep(2000);
                }
            }
        }
    }
}
