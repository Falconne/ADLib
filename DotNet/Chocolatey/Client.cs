using ADLib.Exceptions;
using ADLib.Interactivity;
using ADLib.Logging;
using ADLib.Util;
using System;
using System.Collections.Generic;
using System.IO;

namespace Chocolatey
{
    // TODO parent process must be elevated, check for elevation
    public class Client
    {
        private string _choco;

        private bool _upgraded;

        private readonly IInteractionHandler _interactionHandler;

        public Client(IInteractionHandler interactionHandler)
        {
            _interactionHandler = interactionHandler;
        }


        public Client InstallOrUpgradePackages(params string[] packagesAndArguments)
        {
            var pkgAndArgsList = string.Join(" ", packagesAndArguments);
            GenLog.Info($"Chocolatey Installing / Upgrading: {pkgAndArgsList}");

            var args = new List<object> { "upgrade", "-y" };
            args.AddRange(packagesAndArguments);

            var result = Shell.RunAndGetExitCodeMS(GetChocoExecutable(), args.ToArray());

            GenLog.Info("Chocolatey command done");
            switch (result)
            {
                case 0:
                    break;

                case 1641:
                    _interactionHandler.ExitWithSuccess("Exiting for reboot");
                    break;

                case 3010:
                    AskForRestart();
                    break;

                default:
                    _interactionHandler.ExitWithError("Chocolatey failed");
                    break;
            }

            return this;
        }

        private void AskForRestart()
        {
            if (!_interactionHandler.GetYesNoResponse("A restart is required. Restart now?"))
                return;

            WindowsHost.Restart(0);
            _interactionHandler.ExitWithSuccess("Exiting for reboot");
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
