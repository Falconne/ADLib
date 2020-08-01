using NuGet.Versioning;
using System;
using System.Collections.Generic;

namespace NuGet
{
    public class PackageDefinition
    {
        public readonly string Id;

        public readonly NuGetVersion FullNuGetVersion;

        private Version _marketingVersion;

        public Version MarketingVersion
        {
            get
            {
                if (FullNuGetVersion == null)
                    return null;

                if (_marketingVersion == null)
                    _marketingVersion = GetMarketingVersionFrom(FullNuGetVersion);

                return _marketingVersion;
            }
        }

        public PackageDefinition(string id, NuGetVersion fullNuGetVersion)
        {
            Id = id;

            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new Exception("Cannot parse NuGet package with blank ID");
            }

            FullNuGetVersion = fullNuGetVersion;
        }

        public bool IsFromSameRelease(PackageDefinition other)
        {
            if (FullNuGetVersion == null)
                return false;

            return
                Id == other.Id &&
                MarketingVersion == other.MarketingVersion;
        }

        public bool IsSameId(string id)
        {
            return Id.Equals(id, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is
                   PackageDefinition definition &&
                   Id == definition.Id &&
                   EqualityComparer<NuGetVersion>.Default.Equals(FullNuGetVersion, definition.FullNuGetVersion);
        }

        public override int GetHashCode()
        {
            var hashCode = -612338121;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Id);
            hashCode = hashCode * -1521134295 + EqualityComparer<NuGetVersion>.Default.GetHashCode(FullNuGetVersion);
            return hashCode;
        }

        public override string ToString()
        {
            return $"{Id} @ {FullNuGetVersion}";
        }

        private static Version GetMarketingVersionFrom(NuGetVersion fullNuGetVersion)
        {
            return fullNuGetVersion == null
                ? null
                : new Version(fullNuGetVersion.Major, fullNuGetVersion.Minor, fullNuGetVersion.Patch);
        }

    }
}