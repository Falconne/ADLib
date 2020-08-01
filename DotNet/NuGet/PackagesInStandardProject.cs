using ADLib.Logging;
using NuGet.Versioning;
using System;

namespace NuGet
{
    public class PackagesInStandardProject : PackageCollection
    {
        public readonly string ProjectFile;

        public static PackagesInStandardProject Create(string projectFile)
        {
            var o = new PackagesInStandardProject(projectFile);
            o.Parse();
            return o;
        }

        private PackagesInStandardProject(string projectFile)
        {
            ProjectFile = projectFile;
        }

        protected override void UpdatePackage(PackageDefinition packageDefinition, NuGetVersion toVersion)
        {
            UpdateVersionInFile(ProjectFile, packageDefinition, toVersion);
        }

        protected override void Parse()
        {
            GenLog.Debug($"Parsing for NuGet packages in {ProjectFile}");
            var xelement = GetParsedXml(ProjectFile);
            var packageReferences = xelement.Descendants("PackageReference");
            foreach (var packageReference in packageReferences)
            {
                var id = packageReference.Attribute("Include")?.Value
                         ??
                         packageReference.Attribute("Update")?.Value;

                if (id == null)
                {
                    throw new Exception(
                        $"Invalid syntax in {ProjectFile}: PackageReference with no id");
                }

                var versionRaw = packageReference.Attribute("Version")?.Value;
                if (versionRaw == null)
                {
                    if (id.Equals("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase))
                    {
                        // This one is special. See https://github.com/aspnet/Docs/issues/6430
                        continue;
                    }

                    throw new Exception(
                        $"A specific version has not been provided for {id} in {ProjectFile}");
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