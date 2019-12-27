using AKBDLib.Exceptions;
using AKBDLib.Logging;
using AKBDLib.Util;
using System;
using System.IO;

namespace Chocolatey
{
    // TODO parent process must be elevated, check for elevation
    public class Client
    {
        private string _choco;


        public int InstallOrUpgradePackage(string packageName)
        {
            GenLog.Info($"Installing/Upgrading Chocolatey package {packageName}");

            var result = Shell.RunAndGetExitCodeMS(
                GetChocoExecutable(),
                "upgrade", "-y",
                packageName);

            if (result != 0)
            {
                GenLog.Error($"Exit code was {result}. A reboot or retry may be required.");
            }

            return result;
        }

        private string GetChocoExecutable()
        {
            if (string.IsNullOrWhiteSpace(_choco))
                _choco = FindChocoExecutable();

            return _choco;
        }

        private static string FindChocoExecutable()
        {
            GenLog.Info("Looking for chocolatey");

            var choco = Shell.GetExecutableInPath("choco");
            if (!string.IsNullOrWhiteSpace(choco))
                return choco;

            var allUsersProfile = Environment.GetEnvironmentVariable("ALLUSERSPROFILE");
            if (allUsersProfile == null)
                throw new ConfigurationException("Cannot determine All Users profile directory");

            choco = Path.Combine(allUsersProfile, "chocolatey", "bin", "choco.exe");

            if (File.Exists(choco))
                return choco;

            GenLog.Info("Installing Chocolatey");

            var chocoInstallScript = Path.Combine(Path.GetTempPath(), "choco-install.ps1");
            AKBDLib.Net.Client.DownloadFile(
                "https://chocolatey.org/install.ps1",
                chocoInstallScript);

            Shell.RunPowerShellScriptAndFailIfNotExitZero(chocoInstallScript);

            if (!File.Exists(choco))
                throw new ConfigurationException($"Chocolatey not found in {choco}, please install manually");

            return choco;
        }
    }

}
