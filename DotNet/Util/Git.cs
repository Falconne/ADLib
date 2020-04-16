﻿using ADLib.Exceptions;
using ADLib.Logging;
using System;
using System.IO;

namespace ADLib.Util
{
    public static class Git
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

        public static (int exitCode, string output) Run(params object[] args)
        {
            return Shell.Run(GetClient(), args);
        }

        public static string RunAndFailIfNotExitZero(params object[] args)
        {
            var (exitCode, output) = Run(args);
            if (exitCode == 0)
                return output;

            GenLog.Info(output);
            throw new ConfigurationException($"Git command failed with code {exitCode}");
        }
    }
}