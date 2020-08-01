using ADLib.Logging;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet
{
    public class PackagesInConfigFile : PackageCollection
    {
        public readonly string PackagesConfigFile;

        public readonly IList<string> ProjectFiles = new List<string>();

        public static PackagesInConfigFile Create(string packagesConfigFile)
        {
            var o = new PackagesInConfigFile(packagesConfigFile);
            o.Parse();
            return o;

        }

        private PackagesInConfigFile(string packagesConfigFile)
        {
            PackagesConfigFile = packagesConfigFile;
            var projectDirectory = Path.GetDirectoryName(PackagesConfigFile);
            if (projectDirectory == null)
                throw new Exception($"Unable to determine directory of {PackagesConfigFile}");
            ProjectFiles = Directory.GetFiles(projectDirectory, "*.*proj");
        }

        protected override void UpdatePackage(PackageDefinition packageDefinition, NuGetVersion toVersion)
        {
            UpdateVersionInFile(PackagesConfigFile, packageDefinition, toVersion);
            foreach (var projectFile in ProjectFiles)
            {
                UpdateVersionInFile(projectFile, packageDefinition, toVersion);
            }
        }

        protected override void Parse()
        {
            GenLog.Debug($"Parsing for NuGet packages in {PackagesConfigFile}");

            var xelement = GetParsedXml(PackagesConfigFile);
            var packages = xelement.Descendants("package");
            foreach (var packageElement in packages)
            {
                var id = packageElement.Attribute("id")?.Value;
                var versionRaw = packageElement.Attribute("version")?.Value;
                if (id == null || versionRaw == null)
                {
                    throw new Exception($"Invalid syntax in {PackagesConfigFile}");
                }

                var result = Statics.GetPackageDefinition(id, versionRaw);
                if (result == null)
                    continue;

                GenLog.Debug($"Found {result}");
                Packages.Add(result);
            }
        }
    }
}