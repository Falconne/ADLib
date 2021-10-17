Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module -Force GitHelpers
Import-Module -Force Logging
Import-Module -Force NuGetHelpers
Import-Module -Force TeamCity
Import-Module -Force Util
Import-Module -Force VSProjectUtils

$global:nugetSuffix = $null


function getVersionInfoStructure
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [Version] $version,

        [Parameter(Mandatory = $false)]
        [string] $tag
    )

    $productVersion = [string] $version
    if ($tag)
    {
        $productVersion += " $tag"
    }

    $thisYear = Get-Date -Format yyyy
    $versionInfo = @{
        "Version"              = [string] $version
        "FileVersion"          = [string] $version
        "InformationalVersion" = $productVersion
        # "PackageTags"          = ""
        # "Company"              = ""
        # "Copyright"            = ""
        "AssemblyVersion"      = "$($version.Major).$($version.Minor).$($version.Build).0"
    }

    $global:nugetSuffix = Get-PackageSuffixForDevBranch

    return $versionInfo
}

function setVersionsInDotNetCoreProject
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $csproj,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        $versionInfo
    )

    $originalContent = Get-ContentSafely $csproj
    $newContent = @()

    # Remove existing version tags in csproj and use a standard set
    $versionInfoAdded = $false
    foreach ($line in $originalContent)
    {
        $skipLine = $false
        foreach ($tag in $versionInfo.Keys)
        {
            if ($line.Contains("<$tag>"))
            {
                $skipLine = $true
                break
            }
        }

        if (!$skipLine)
        {
            if ($line.Contains("</PropertyGroup>") -and $newContent[-1].Contains("<PropertyGroup>"))
            {
                # Remove empty PropertyGroups left over by removing existing version info
                $newContent = $newContent[0..($newContent.Length - 2)]
                continue
            }

            $newContent += $line
            if (!$versionInfoAdded -and $line.Contains("<Project"))
            {
                $newContent += "  <PropertyGroup>"
                foreach ($tag in $versionInfo.Keys)
                {
                    $newContent += "    <$tag>$($versionInfo[$tag])</$tag>"
                }

                if ($global:nugetSuffix)
                {
                    $projectName = (Get-Item $csproj).Basename
                    $id = "$projectName-$($global:nugetSuffix)"
                    Write-Host "Overriding package ID for dev branch: $id"
                    $newContent += "  <PackageId>$id</PackageId>"
                }

                $newContent += "  </PropertyGroup>"
                $versionInfoAdded = $true
            }
        }
    }

    if ($originalContent -ne $newContent)
    {
        Write-Host "Updating version to $($versionInfo["Version"]) in $csproj"
        Write-LinesToFileSafely -Path $csproj -Content $newContent
    }
}

function setVersionsInServiceFabricApplications
{
    param
    (
        # Manifest File
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $manifest,

        # Version object
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $version
    )

    $content = Get-Content $manifest

    $content = $content -Replace "ApplicationTypeVersion=`"[\d\.]+", `
        "ApplicationTypeVersion=`"$version"

    $content = $content -Replace "ServiceManifestVersion=`"[\d\.]+", `
        "ServiceManifestVersion=`"$version"

    $content = $content -Replace " Version=`"[\d\.]+", `
        " Version=`"$version"

    $content = $content -Replace "<\?xml version=`"$version", `
        "<?xml version=`"1.0"

    Set-Content -Encoding UTF8 $manifest $content
}

function setVersionInRCFile
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $file,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        $versionInfo
    )

    $originalContent = Get-Content $file
    $content = $originalContent
    $rcVersion = $versionInfo["Version"].Replace(".", ",")

    $content = $content -Replace "FILEVERSION (\d+,){3}\d+", `
        "FILEVERSION $rcVersion"

    $content = $content -Replace "PRODUCTVERSION (\d+,){3}\d+", `
        "PRODUCTVERSION $rcVersion"

    $content = $content -Replace "`"FileVersion`",\s*`"(\d+[\.,]\s*){3}\d+", `
        "`"FileVersion`", `"$($versionInfo["FileVersion"])"

    $content = $content -Replace "`"ProductVersion`",\s*`"[^`"]*`"", `
        "`"ProductVersion`", `"$($versionInfo["InformationalVersion"])\0`""

    $content = $content -Replace "`"CompanyName`",\s*`".*[^(\\0)]`"", `
        "`"CompanyName`", `"$($versionInfo["Company"])`""

    $content = $content -Replace "`"CompanyName`",\s*`".*\\0`"", `
        "`"CompanyName`", `"$($versionInfo["Company"])\0`""

    $content = $content -Replace "`"LegalCopyright`",\s*`".*[^(\\0)]`"", `
        "`"LegalCopyright`", `"$($versionInfo["Copyright"])`""

    $content = $content -Replace "`"LegalCopyright`",\s*`".*\\0`"", `
        "`"LegalCopyright`", `"$($versionInfo["Copyright"])\0`""

    if ($originalContent -ne $content)
    {
        Write-Host "Updating version to $($versionInfo["Version"]) in $file"
        Set-Content $file $content
    }
}

function setVersionInAssemblyInfo
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $file,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        $versionInfo
    )

    $content = Get-Content $file

    foreach ($tag in $versionInfo.Keys)
    {
        if ($tag -eq "Version")
        {
            continue
        }

        $content = $content -Replace "^\s*\[assembly: Assembly$tag\(.+?\)\]", `
            "[assembly: Assembly$tag(`"$($versionInfo[$tag])`")]"
    }

    $content = $content -Replace "^\s*\[assembly: AssemblyVersion\(.+?\)\]", `
        "[assembly: AssemblyVersion(`"$($versionInfo["AssemblyVersion"])`")]"

    if (!($content -match "^\s*\[assembly: AssemblyInformationalVersion"))
    {
        $content += "[assembly: AssemblyInformationalVersion(`"$($versionInfo["InformationalVersion"])`")]"
    }

    Write-Host "Updating version to $($versionInfo["Version"]) in $file"
    Set-Content -Encoding UTF8 $file $content
}

function setVersionInAngularEnvironmentFile
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $file,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        $versionInfo
    )

    Write-Host "Informational version is $($versionInfo["InformationalVersion"])"

    $contents = (Get-Content $file | Out-String)

    $environment = ($contents -replace "(?ms)(// VERSION START.*// VERSION END\r?\n|// INJECT VERSION)", "")

    if ($environment.Length -eq $contents.Length)
    {
        return;
    }

    $environment += @"
// VERSION START
if (environment) {
    environment.version = '$($versionInfo["InformationalVersion"])';
    environment.copyright = '$($versionInfo["Copyright"])';
}
// VERSION END
"@

    Write-Host "Setting content of $file to:"
    Write-Host $environment
    Set-Content $file $environment
}

function Set-VersionInAllFiles
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [Version] $version,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $searchRoot,

        [Parameter(Mandatory = $false)]
        [string] $tag
    )

    Write-BlockStart "Updating version info inside $searchRoot"
    $versionInfo = getVersionInfoStructure $version $tag
    Write-Host $versionInfo
    Push-Location $searchRoot

    try
    {
        Write-Host "Updating version info in csproj files"
        Invoke-Git ls-files "*.csproj" | `
                ? { Test-Is2017FormatProject $_ } | `
                % { setVersionsInDotNetCoreProject $_ $versionInfo }

        Write-Host "Updating version info in AssemblyInfo.cs files"
        Invoke-Git ls-files "*/AssemblyInfo.cs" | `
                % { setVersionInAssemblyInfo $_ $versionInfo }

        Write-Host "Updating version info in .rc* files"
        Invoke-Git ls-files "*.rc*" | `
                % { setVersionInRCFile $_ $versionInfo }

        Write-Host "Updating version info in angular projects"
        Invoke-Git ls-files "*/environment.ts" | `
                % { setVersionInAngularEnvironmentFile $_ $versionInfo }

        Invoke-Git ls-files "*/environment.prod.ts" | `
                % { setVersionInAngularEnvironmentFile $_ $versionInfo }

        Write-Host "Updating version info in Service Fabric projects"
        Invoke-Git ls-files "*/ApplicationManifest.xml" "*/ServiceManifest.xml" | `
                % { setVersionsInServiceFabricApplications $_ $version }
    }
    catch
    {
        $_ | Select-Object * | Out-Host
        throw "Version injection failed"
    }
    finally
    {
        Pop-Location
    }

    Write-BlockEnd "Updating version info inside $searchRoot"
}

function Set-VersionIfInTeamCity
{
    param
    (
        # Search root (uses parent script root if not given)
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]
        $SearchRoot
    )

    if (!(Test-IsInsideTeamCity))
    {
        Write-Host "Skipping versioning as not currently in TeamCity"
        return
    }

    $version = Get-BuildNumberInTeamCityBuild "1.0.0.1"

    if (!$SearchRoot)
    {
        $SearchRoot = Split-Path -Parent $MyInvocation.PSCommandPath
    }

    Set-VersionInAllFiles -version $version -searchRoot $SearchRoot
}

Export-ModuleMember -Function *-*
