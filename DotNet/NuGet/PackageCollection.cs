using ADLib.Logging;
using ADLib.Util;
using NuGet.Versioning;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace NuGet
{
    public abstract class PackageCollection
    {
        public readonly List<PackageDefinition> Packages = new List<PackageDefinition>();

        public void UpdatePackage(string id, NuGetVersion toVersion)
        {
            var currentPackageDefinition = Packages.FirstOrDefault(pd => pd.Id == id);
            UpdatePackage(currentPackageDefinition, toVersion);
        }

        protected abstract void UpdatePackage(PackageDefinition packageDefinition, NuGetVersion toVersion);

        protected void UpdateVersionInFile(string path, PackageDefinition package, NuGetVersion toVersion)
        {
            var fileContent = File.ReadAllLines(path);
            var changesMade = false;
            var marker = new Regex($@"\b{package.Id}\b", RegexOptions.IgnoreCase);
            for (var i = 0; i < fileContent.Length; i++)
            {
                var line = fileContent[i];
                if (!marker.IsMatch(line))
                    continue;
                changesMade = true;
                fileContent[i] = line.Replace(package.FullNuGetVersion.ToString(), toVersion.ToString());
            }

            if (!changesMade)
                return;

            GenLog.Info($"Updating {package.Id} {package.FullNuGetVersion} => {toVersion} in {path}");
            FileSystem.WriteToFileSafely(path, fileContent);
        }

        protected abstract void Parse();

        protected static XDocument GetParsedXml(string file)
        {
            XDocument xelement;
            try
            {
                xelement = XDocument.Parse(File.ReadAllText(file));
            }
            catch (XmlException e)
            {
                GenLog.Error($"XML Exception while loading {file}: {e.Message}");
                throw;
            }

            return xelement;
        }
    }
}