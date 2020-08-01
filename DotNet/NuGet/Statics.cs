using ADLib.Logging;
using ADLib.Net;
using ADLib.Util;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet
{
    public static class Statics
    {
        public static IEnumerable<PackageCollection> GetPackageCollectionsUnder(string root)
        {
            GenLog.Info($"Looking for NuGet packages used under {root}");

            var pkgConfigFiles = Directory.GetFiles(root, "packages.config", SearchOption.AllDirectories)
                .Where(ShouldProcessDirectoryContaining);

            foreach (var file in pkgConfigFiles)
            {
                yield return PackagesInConfigFile.Create(file);
            }

            var projects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
                .Where(ShouldProcessProject);

            foreach (var file in projects)
            {
                yield return PackagesInStandardProject.Create(file);
            }
        }

        public static PackageDefinition GetPackageDefinition(string packageId, string versionRaw)
        {
            try
            {
                var version = NuGetVersion.Parse(versionRaw);
                return new PackageDefinition(packageId, version);
            }
            catch (Exception)
            {
                GenLog.Warning($"Ignoring unparsable version for {packageId}: {versionRaw}");
            }

            return null;
        }

        public static (bool result, string stdout, string stderr)
            RunNuGet(params string[] arguments)
        {
            var nuget = GetNuGet();
            var argsJoined = string.Join(" ", arguments);
            GenLog.Info($"Running: {nuget} {argsJoined}");

            // ReSharper disable once CoVariantArrayConversion
            var command = Medallion.Shell.Command.Run(nuget, arguments);
            var stdout = command.StandardOutput.ReadToEnd();
            var stderr = command.StandardError.ReadToEnd();
            GenLog.Info(stdout);
            GenLog.Info(stderr);
            GenLog.Info(command.Result.Success ? "Command success" : "Command failed!");

            return (command.Result.Success, stdout, stderr);
        }

        private static string GetNuGet()
        {
            var nugetDir = $@"{Shell.GetScriptDir()}\nuget";
            var nuget = $@"{nugetDir}\nuget.exe";
            if (File.Exists(nuget))
                return nuget;

            return Client.DownloadFile(
                "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe",
                nuget);
        }

        private static bool ShouldProcessDirectoryContaining(string path)
        {
            var directoryToCheck = Path.GetDirectoryName(path);
            if (directoryToCheck == null)
                return false;

            while (true)
            {
                var skipMarker = Path.Combine(directoryToCheck, "SkipNuGetValidation");
                if (File.Exists(skipMarker))
                    return false;

                directoryToCheck = Directory.GetParent(directoryToCheck)?.FullName;
                if (directoryToCheck == null)
                    return true;
            }
        }

        private static bool ShouldProcessProject(string path)
        {
            if (!ShouldProcessDirectoryContaining(path))
                return false;

            var csprojContent = File.ReadAllText(path);
            return csprojContent.Contains("PackageReference");
        }
    }
}
