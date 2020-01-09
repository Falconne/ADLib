using ADLib.Exceptions;
using ADLib.Logging;
using ADLib.Util;
using System;
using System.IO;

namespace Chocolatey
{
    // TODO parent process must be elevated, check for elevation
    public class Client
    {
        private string _choco;

        private bool _upgraded;


        public int InstallOrUpgradePackages(params string[] packages)
        {
            var pkgList = string.Join(" ", packages);
            GenLog.Info($"Installing/Upgrading Chocolatey packages: {pkgList}");

            var result = Shell.RunAndGetExitCodeMS(
                GetChocoExecutable(),
                "upgrade", "-y",
                packages);

            if (result != 0)
            {
                GenLog.Error($"Exit code was {result}. A reboot or retry may be required.");
            }

            return result;
        }

        private string GetChocoExecutable()
        {
            if (string.IsNullOrWhiteSpace(_choco))
            {
                _choco = FindChocoExecutable();
                if (!_upgraded)
                {
                    GenLog.Info("Upgrading Chocolatey");
                    InstallOrUpgradePackages("chocolatey");
                    _upgraded = true;
                }
            }

            return _choco;
        }

        private string FindChocoExecutable()
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
            ADLib.Net.Client.DownloadFile(
                "https://chocolatey.org/install.ps1",
                chocoInstallScript);

            Shell.RunPowerShellScriptAndFailIfNotExitZero(chocoInstallScript);

            if (!File.Exists(choco))
                throw new ConfigurationException($"Chocolatey not found in {choco}, please install manually");

            _upgraded = true;
            return choco;
        }
    }

}
