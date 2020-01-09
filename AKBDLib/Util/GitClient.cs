using AKBDLib.Exceptions;
using AKBDLib.Logging;
using System;
using System.IO;

namespace AKBDLib.Util
{
    public class GitClient
    {
        private string _path;

        public string GetIfFound()
        {
            if (!string.IsNullOrWhiteSpace(_path))
                return _path;

            _path = Shell.GetExecutableInPath("git");

            if (string.IsNullOrWhiteSpace(_path))
                _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "git.exe");

            if (!File.Exists(_path))
                _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "git.exe");

            if (!File.Exists(_path))
            {
                GenLog.Error("git.exe not found in any standard location");
                _path = null;
            }

            return _path;
        }

        public string Get()
        {
            GetIfFound();
            if (string.IsNullOrWhiteSpace(_path))
                throw new ConfigurationException("git.exe not found in any standard location");

            return _path;
        }
    }
}