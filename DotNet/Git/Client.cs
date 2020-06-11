using ADLib.Exceptions;
using ADLib.Logging;
using ADLib.Util;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ADLib.Git
{
    public static class Client
    {
        private static string _gitPath;


        public static string GetClientIfFound()
        {
            if (!string.IsNullOrWhiteSpace(_gitPath))
                return _gitPath;

            _gitPath = Shell.GetExecutableInPath("git");

            if (string.IsNullOrWhiteSpace(_gitPath))
                _gitPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\git\bin\git.exe";

            if (!File.Exists(_gitPath))
                _gitPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\git\bin\git.exe";

            if (!File.Exists(_gitPath))
            {
                GenLog.Error("git.exe not found in any standard location");
                _gitPath = null;
            }

            return _gitPath;
        }

        public static string GetClient()
        {
            GetClientIfFound();
            if (string.IsNullOrWhiteSpace(_gitPath))
                throw new ConfigurationException("git.exe not found in any standard location");

            return _gitPath;
        }

        public static (int exitCode, string stdout, string stderr) Run(params string[] args)
        {
            return RunAsync(args).Result;
        }

        public static async Task<(int exitCode, string stdout, string stderr)> RunAsync(
            params string[] args)
        {
            return await Shell.RunAsync(GetClient(), args);
        }

        public static (string stdout, string stderr) RunAndFailIfNotExitZero(params string[] args)
        {
            return RunAndFailIfNotExitZeroAsync(args).Result;
        }

        public static async Task<(string stdout, string stderr)> RunAndFailIfNotExitZeroAsync(
            params string[] args)
        {
            var (exitCode, stdout, stderr) = await RunAsync(args);
            if (exitCode == 0)
                return (stdout, stderr);

            throw new ConfigurationException($"Git command failed with code {exitCode}");
        }
    }
}