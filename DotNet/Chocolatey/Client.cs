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
            return Run("upgrade", packagesAndArguments);
        }

        public Client InstallPackages(params string[] packagesAndArguments)
        {
            return Run("install", packagesAndArguments);
        }

        private Client Run(string cmd, params string[] packagesAndArguments)
        {
            var args = new List<object> { cmd, "-y" };
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

        public string GetChocoExecutable()
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

        private void AskForRestart()
        {
            var query = "A restart is required. Restart now? You may need to re-run the current script after the restart to complete your installation.";
            if (!_interactionHandler.GetYesNoResponse(query))
                return;

            WindowsHost.Restart(5);
            _interactionHandler.ExitWithSuccess("Exiting for reboot");
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
