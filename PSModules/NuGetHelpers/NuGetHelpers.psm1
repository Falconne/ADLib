Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module -Force Util
Import-Module -Force Logging

# Fetch latest nuget.exe from the internet
function Get-NuGet
{
    $nuget = "$PSScriptRoot\nuget.exe"
    $retriesAllowed = 5
    while (!(Test-Path $nuget))
    {
        try
        {
            $url = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
            Write-Host "Fetching latest NuGet from $url into $nuget"
            Save-FileFromNetwork $url $nuget
            Write-Host "Done"
        }
        catch
        {
            if ($retriesAllowed -le 0)
            {
                throw
            }

            $retriesAllowed--
            Write-Host "Download failed. Will retry..."
            Start-Sleep -Seconds 10
        }
    }

    return $nuget
}

# Run this before using NuGet to download PowerShell modules, as that requires
# a minimum version of the NuGet provider
function Update-NuGetProvider
{
    Write-Host "Checking NuGet provider"
    try
    {
        $provider = Get-PackageProvider -Name NuGet -Force
        if ($provider)
        {
            $minVersion = [System.Version] "2.8.5.208"
            if ($provider.Version -ge $minVersion)
            {
                return
            }
        }
    }
    catch
    {
    }
    Write-Host "Installing NuGet provider"
    Install-PackageProvider -Name NuGet -Scope CurrentUser -Force -Confirm
}


function Enable-PSGallery
{
    Update-NuGetProvider
    if ((Get-PSRepository -Name PSGallery).InstallationPolicy -ne "Trusted")
    {
        Write-Host "Set PSGallery as a trusted repository"
        Set-PSRepository -Name PSGallery -InstallationPolicy Trusted -ErrorAction SilentlyContinue
    }
}


# Restore NuGet packages for given sln file
function Restore-NuGetPackages
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Solution,

        [Parameter(Mandatory = $false)]
        [switch]
        $IgnoreSolutionVersion
    )

    Write-BlockStart "Restoring nuget packages for $Solution"
    $nuget = Get-NuGet
    if ($IgnoreSolutionVersion)
    {
        $msbuildDir = (Get-Item (Get-MSBuildLocation)).Directory.FullName
    }
    else
    {
        $msbuildDir = (Get-Item (Get-MSBuildLocation -ForSolution $Solution)).Directory.FullName
    }

    $retriesAllowed = 3
    while ($true)
    {
        Write-Host "$nuget restore -MSBuildPath $msbuildDir $Solution"
        & $nuget restore -MSBuildPath $msbuildDir $Solution
        if ($LASTEXITCODE -eq 0)
        {
            break
        }

        if ($retriesAllowed -le 0)
        {
            throw "Error during nuget restore"
        }

        $retriesAllowed--
        Write-Warning "Error during nuget restore, will retry..."
        Start-Sleep -Seconds 5
    }
    Write-BlockEnd "Restoring nuget packages for $Solution"
}

function Get-NuGetToolsDownloadRoot
{
    return "$($Env:TEMP)\NuGetTools"
}

# Download a tool directly from public NuGet repo
function Install-NuGetToolPackage
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name,

        [Parameter(Mandatory = $false)]
        [string]
        $Version,

        [Parameter(Mandatory = $false)]
        [switch]
        $NoCache
    )

    if ($Version)
    {
        $progressMessage = "Installing nuget package $Name @ $version"
    }
    else
    {
        $progressMessage = "Installing latest nuget package $Name"
    }

    Write-BlockStart $progressMessage
    $nuget = Get-NuGet
    $downloadLocation = Get-NuGetToolsDownloadRoot

    $argList = @("install", $Name, "-OutputDirectory", $downloadLocation)

    if ($Version)
    {
        $argList += "-Version"
        $argList += $Version
    }
    else
    {
        $argList += "-ExcludeVersion"
    }

    if ($NoCache)
    {
        $argList += "-NoCache"
    }

    Write-Host "Running: $nuget $argList"

    Push-Location "$PSScriptRoot\..\..\..\"

    $retriesAllowed = 3
    $result = ""
    foreach ($i in 0..$retriesAllowed)
    {
        try
        {
            # Prevent output becoming part of function call result
            $result = & $nuget $argList
            Write-Host $result
            if ($LASTEXITCODE -ne 0)
            {
                throw "Error during nuget restore"
            }
            break
        }
        catch
        {
            if ($_ -match "Unable to find package '(.+)'")
            {
                $packageToReset = $matches[1]
                Write-Host "$packageToReset has been unlisted, will delete local copy"
                $dirToDelete = "$downloadLocation\$packageToReset"
                if (Test-Path $dirToDelete)
                {
                    Remove-Path $dirToDelete
                }
                else
                {
                    Write-Warning "$dirToDelete not found!"
                }
            }
            else
            {
                Write-Host $result
                Write-FullException $_

                if ($i -eq 0)
                {
                    Write-Host "Will try clearning caches"
                    Clear-NuGetCaches
                }
            }

            if ($i -ge $retriesAllowed)
            {
                throw
            }

            Write-Host "Will retry..."
            Start-Sleep -Seconds 5
        }
    }

    Pop-Location

    Write-BlockEnd $progressMessage

    return Get-NuGetToolsDownloadRoot
}

function Get-NuGetToolPackageDownloadLocation($name, $version)
{
    if ($version)
    {
        $name = "$name.$version"
    }

    return "$(Get-NuGetToolsDownloadRoot)\$name\tools"
}

function Get-NuGetContentPackageDownloadLocation($name, $version)
{
    if ($version)
    {
        $name = "$name.$version"
    }

    return "$(Get-NuGetToolsDownloadRoot)\$name\contentFiles\any\any"
}

function Get-LatestNuGetToolPackage
{
    param
    (
        # Tool id on NuGet
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name,

        # Optionally append tool's primary file to return value
        [Parameter(Mandatory = $false)]
        [string]
        $PrimaryFile,

        [Parameter(Mandatory = $false)]
        [switch]
        $NoCache
    )

    $result = Install-NuGetToolPackage $Name -NoCache:$NoCache
    $toolLocation = Get-NuGetToolPackageDownloadLocation $Name
    if (!(Test-Path $toolLocation))
    {
        Write-Host $result
        throw "Unable to fetch $name from NuGet into $toolLocation"
    }

    $returnPath = $toolLocation
    if ($PrimaryFile)
    {
        $returnPath += "\$PrimaryFile"
        if (!(Test-Path $returnPath))
        {
            throw "$returnPath not found"
        }
    }

    return $returnPath
}

# Use this to, for example, fetch a package's latest version inside a specific
# product version, e.g. <= 10.1.0.99999
function Get-LatestNuGetToolPackageBelowBaseline
{
    param
    (
        # Tool id on NuGet
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name,

        # Minimum version to accept
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [Version]
        $BaselineVersion
    )

    $latestVersionBelowBaseline = Get-LatestVersionOfPackageBelowBaseline `
        -Name $Name -BaselineVersion $BaselineVersion

    if (!$latestVersionBelowBaseline)
    {
        throw "No $Name NuGet package found below baseline version $BaselineVersion"
    }

    $result = Install-NuGetToolPackage -Name $Name -Version $latestVersionBelowBaseline
    $toolLocation = Get-NuGetToolPackageDownloadLocation $Name -Version $latestVersionBelowBaseline
    if (!(Test-Path $toolLocation))
    {
        Write-Host $result
        throw "Unable to fetch $name from NuGet into $toolLocation"
    }

    return $toolLocation
}

function Get-NuGetToolPackage
{
    param
    (
        # Package name to fetch
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name,

        # Version to fetch
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Version
    )

    $result = Install-NuGetToolPackage $Name $Version
    $toolLocation = Get-NuGetToolPackageDownloadLocation $Name $Version
    if (!(Test-Path $toolLocation))
    {
        Write-Host $result
        throw "Unable to fetch $Name from NuGet into $toolLocation"
    }

    return $toolLocation
}

function Get-NuGetContentPackage
{
    param
    (
        # Tool id on NuGet
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name,

        # Version, if not latest
        [Parameter(Mandatory = $false)]
        [string]
        $Version = $null,

        # Optionally, opy content dir to this location
        [Parameter(Mandatory = $false)]
        [string]
        $Destination
    )

    $result = Install-NuGetToolPackage -Name $Name -Version $Version
    $toolLocation = Get-NuGetContentPackageDownloadLocation $Name $Version
    if (!(Test-Path $toolLocation))
    {
        Write-Host $result
        throw "Unable to fetch $name from NuGet into $toolLocation"
    }

    if (!$Destination)
    {
        return $toolLocation
    }

    New-Directory $Destination
    Copy-WithMirror $toolLocation $Destination
}

# Use this to, for example, fetch a package's latest version inside a specific
# product version, e.g. <= 10.1.0.99999
function Get-LatestNuGetContentPackageBelowBaseline
{
    param
    (
        # Tool id on NuGet
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name,

        # Minimum version to accept
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [Version]
        $BaselineVersion,

        # Optionally, opy content dir to this location
        [Parameter(Mandatory = $false)]
        [string]
        $Destination
    )

    $latestVersionBelowBaseline = Get-LatestVersionOfPackageBelowBaseline `
        -Name $Name -BaselineVersion $BaselineVersion

    if (!$latestVersionBelowBaseline)
    {
        throw "No $Name NuGet package found below baseline version $BaselineVersion"
    }

    return Get-NuGetContentPackage -Name $Name -Version $latestVersionBelowBaseline $Destination
}


# Create packages used by shell scripts to download build tools, etc
function New-GeneratedNuGetToolPackage
{
    param
    (
        # Directory to package
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Directory,

        # Package ID (name)
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $ID,

        # Description for package
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Description,

        # Package build version
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Version,

        # Location to build package to
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $OutputDirectory,

        # Optional set of content files
        [Parameter(Mandatory = $false)]
        [string]
        $ContentFilesDirectory
    )

    Write-Progress "Generating nuget package from $Directory"
    $nuspec = "$ID.nuspec"
    $contentFilesTag = ""
    if ($ContentFilesDirectory)
    {
        $contentFilesTag = "<file src=`"$ContentFilesDirectory\**`" target=`"contentFiles`" />"
    }

    $nuspecContent = @"
<?xml version="1.0"?>
<package>
    <metadata>
        <id>$ID</id>
        <version>$Version</version>
        <authors>ADLib</authors>
        <owners>Anuradha Dissanayake</owners>
        <requireLicenseAcceptance>false</requireLicenseAcceptance>
        <description>$Description</description>
        <tags>ADLib</tags>
        <contentFiles>
            <files include="**" buildAction="None" copyToOutput="true" flatten="true" />
        </contentFiles>
    </metadata>
    <files>
        <file src="$Directory\**" target="tools\" exclude="*.nuspec" />
        $contentFilesTag
    </files>
</package>
"@
    Set-Content $nuspec $nuspecContent
    New-NuGetPackage -Directory (Get-Location) -OutputDirectory $OutputDirectory
    Remove-Path $nuspec
}

# Create package that will copy its contents to parent project's build output
function New-GeneratedNuGetArtifactPackage
{
    param
    (
        # Directory to package
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Directory,

        # Package ID (name)
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $ID,

        # Description for package
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Description,

        # Package build version
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Version,

        # Location to build package to
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $OutputDirectory
    )

    Write-Progress "Generating nuget package from $Directory"
    $nuspec = "$ID.nuspec"
    $targetsFile = "$ID.targets"

    $nuspecContent = @"
<?xml version="1.0"?>
<package>
    <metadata>
        <id>$ID</id>
        <version>$Version</version>
        <authors>ADlib</authors>
        <owners>Anuradha Dissanayake</owners>
        <requireLicenseAcceptance>false</requireLicenseAcceptance>
        <description>$Description</description>
        <tags>ADLib</tags>
    </metadata>
    <files>
        <file src=`"$Directory\**`" target=`"build\`" />
        <file src=`"$targetsFile`" target=`"build\`" />
    </files>
</package>
"@
    Set-Content $nuspec $nuspecContent

    $targetsFileContent = @"
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup>
    <ContentsToCopy Include="`$(MSBuildThisFileDirectory)**\*.*" />
    <None Include="@(ContentsToCopy)">
      <Link>%(RecursiveDir)%(FileName)%(Extension)</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
"@

    Set-Content $targetsFile $targetsFileContent

    New-NuGetPackage -Directory (Get-Location) -OutputDirectory $OutputDirectory -NuSpec $nuspec
    Remove-Path $nuspec
    Remove-Path $targetsFile
}

# Create packages used to download content files during build
function New-GeneratedNuGetContentPackage
{
    param
    (
        # Directory to package
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Directory,

        # Package ID (name)
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $ID,

        # Description for package
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Description,

        # Package build version
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Version,

        # Location to build package to
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $OutputDirectory,

        [Parameter(Mandatory = $false)]
        [string]
        $BuildAction = "None",

        [Parameter(Mandatory = $false)]
        [string]
        $CopyToOutput = "true"
    )

    Write-Progress "Generating nuget package from $Directory"
    $nuspec = "$ID.nuspec"

    $nuspecContent = @"
<?xml version="1.0"?>
<package>
    <metadata>
        <id>$ID</id>
        <version>$Version</version>
        <authors>ADlib</authors>
        <owners>Anuradha Dissanayake</owners>
        <requireLicenseAcceptance>false</requireLicenseAcceptance>
        <description>$Description</description>
        <tags>ADLib</tags>
        <contentFiles>
            <files include="**" buildAction="$BuildAction" copyToOutput="$CopyToOutput" flatten="true" />
        </contentFiles>
    </metadata>
    <files>
        <file src=`"$Directory\**`" target=`"contentFiles\any\any`" />
        <file src=`"$Directory\**`" target=`"content`" />
    </files>
</package>
"@
    Set-Content $nuspec $nuspecContent
    New-NuGetPackage -Directory (Get-Location) -OutputDirectory $OutputDirectory
    Remove-Path $nuspec
}

function New-NuGetPackage
{
    param
    (
        # Directory containing .nuspec file
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Directory,

        # Location to build package to
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $OutputDirectory,

        # Override version in .nuspec file
        [Parameter(Mandatory = $false)]
        [string]
        $Version,

        # Specific nuspec file to use
        [Parameter(Mandatory = $false)]
        [string]
        $NuSpec,

        [Parameter(Mandatory = $false)]
        [switch]
        $NoDefaultExcludes
    )

    $argList = @("pack")

    if ($NuSpec)
    {
        $argList += $NuSpec
    }

    $argList += "-OutputDirectory", $OutputDirectory

    if ($Version)
    {
        $argList += "-Version", $Version
    }

    if ($NoDefaultExcludes)
    {
        $argList += "-NoDefaultExcludes"
    }

    $nuget = Get-NuGet
    Push-Location $Directory
    Write-Host "Running pack from $Directory"
    try
    {
        & $nuget @argList
        if ($LASTEXITCODE -ne 0)
        {
            throw "Packing of $Directory failed"
        }
    }
    catch
    {
        throw
    }
    finally
    {
        Pop-Location
    }
}

# Get a package's latest version of a package that's <= a baseline version.
function Get-LatestVersionOfPackageBelowBaseline
{
    param
    (
        # Package name
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name,

        # Minimum version to accept
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [Version]
        $BaselineVersion
    )

    Write-Host "Looking for latest version of $Name <= $BaselineVersion"
    $nugetArgs = @("list")
    $nugetArgs += "-AllVersions"

    Write-Host (Get-NuGet) @nugetArgs
    $allVersions = & (Get-NuGet) @nugetArgs | `
        ConvertFrom-Csv -Delimiter " " -Header "Name", "Version" | `
        ? { $_.Name -eq $Name }

    Write-Host $allVersions

    if (!$allVersions)
    {
        Write-Host "No packages found"
        return $null
    }

    $version = $allVersions | `
        % { [Version] $_.Version } | `
        ? { $_ -le $BaselineVersion } | `
        Sort | Select -Last 1

    if (!$version)
    {
        Write-Host "Not found"
    }
    else
    {
        Write-Host "Found $version"
    }

    return $version
}

function Clear-NuGetCaches
{
    Write-Host "Clearing local NuGet caches"
    & (Get-NuGet) locals -clear all
}

Export-ModuleMember -Function *-*
