Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module -Force GitHelpers
Import-Module -Force NuGetHelpers
Import-Module -Force Logging
Import-Module -Force Util


# Check given output for error patterns and push them to TeamCity
function Test-ForErrors
{
    param
    (
        [Parameter(Mandatory = $true, HelpMessage = "Build Output")] $output
    )

    $errorPatterns = @(
        ": error"
        "Could not find file"
        " fatal error"
        "Cannot delete directory"
        "could not be found with source path"
        "The process cannot access the file"
        "Error: "
        "SOLUTION FAILED:"
        "Cannot delete directory"
        "Error \d+"
        ": Cannot open file:"
        "ERROR in "
        "CSC : error"
    )

    $foundErrors = $output | `
        Select-String -Pattern $errorPatterns -CaseSensitive | `
        Out-String | % { $_.Split("`r`n") } | % { $_.Trim() } | ? { $_ }

    if ($foundErrors)
    {
        Write-Host "--------------------------------------------------------------------------------"
        Write-Host "START: Error Pattern Summary`r`n"

        $foundErrors | % { Write-CustomError $_ }

        Write-Host "`r`nEND: Error Pattern Summary"
        Write-Host "--------------------------------------------------------------------------------"
    }
}

# If we see these patterns, retry the specific build job that produced it
function Test-ForTransientErrors
{
    param
    (
        [Parameter(Mandatory = $true, HelpMessage = "Build Output")] $output
    )

    $errorPatterns = @(
        "error LGHT0001: Cannot create a file when that file already exists"
        "error CNDL0001 : The process cannot access the file"
        "fatal error LNK1103: debugging information corrupt; recompile module"
        "fatal error LNK1318: Unexpected PDB error; OK"
        "fatal error C1001: An internal error has occurred in the compiler"
        "corrupt PDB file"
    )

    $foundErrors = $output | Select-String -pattern $errorPatterns | `
        Out-String | % { $_.Split("`r`n") } | % { $_.Trim() } | ? { $_ }

    if ($foundErrors)
    {
        Write-Host "--------------------------------------------------------------------------------"
        Write-Host "Warning: Transient errors found, should retry:"
        $foundErrors | % { Write-TeamCityMessage -Text $_ -Status "WARNING" }
        Write-Host "--------------------------------------------------------------------------------"

        foreach ($foundError in $foundErrors)
        {
            if ($foundError -inotmatch "([\w\-. ]+\.(obj|pdb))")
            {
                continue
            }

            $fileToDelete = $matches[1]
            Write-Host "Deleting $fileToDelete"
            Push-Location (Get-ParentRepoAnchorPoint)
            Invoke-Git ls-files -o **/$fileToDelete | % { Remove-Path $_ }
            Pop-Location
        }
    }

    return $foundErrors
}


# Waits for background jobs to complete
function Wait-ForJobs
{
    param
    (
        # If set, only waits for jobs with names that contain this string.
        # Otherwise wait for all background jobs
        [Parameter (Mandatory = $false)]
        [string]
        $JobText = $null
    )

    $feedback = "Waiting for all background jobs..."
    if ($jobText) { $feedback = "Waiting for $jobText jobs..." }

    $lastCheck = Get-Date
    Write-BlockStart $feedback
    While ($true)
    {
        $s = New-TimeSpan $lastCheck $(Get-Date)
        $runningJobs = @(Get-Job -State Running)
        if ($jobText)
        {
            $runningJobs = @($runningJobs | ? { $_.Name.Contains($jobText) })
        }

        if ([int] $s.TotalSeconds % 30 -eq 0)
        {
            $lastCheck = Get-Date

            $runningJobs | % { Write-Host "$($_.Name)" }
            Write-Host "--------------------------------------------------------------------------------"
        }

        if (!$runningJobs -or $runningJobs.Length -eq 0)
        {
            break
        }

        Start-Sleep -s 1
    }

    $failedJobs = @(Get-Job -State Failed)
    if ($jobText)
    {
        $failedJobs = @($failedJobs | ? { $_.Name.Contains($jobText) })
    }

    $transientErrorFound = $false
    if ($failedJobs -and $failedJobs.Length -gt 0)
    {
        $fatalErrorFound = $false

        foreach ($job in $failedJobs)
        {
            Write-Host "================================================================================"
            Write-Host "START: Failure Log $($job.Name)"
            Write-Host "--------------------------------------------------------------------------------"
            try
            {
                Write-CustomError ($job.ChildJobs[0].JobStateInfo.Reason.Message)
                Write-CustomError ($job.ChildJobs[0].Error)
            }
            catch
            {
            }
            Write-Host "--------------------------------------------------------------------------------"

            Receive-Job -Job $job -OutVariable output -ErrorVariable errorOutput -ea SilentlyContinue
            $output | Out-String | Out-Host
            $errorOutput | Out-String | Out-Host
            Test-ForErrors $output

            Write-Host "================================================================================"
            Write-Host "END: Failure Log $($job.Name)"
            Write-Host "--------------------------------------------------------------------------------"

            if (!(Test-ForTransientErrors $output))
            {
                $fatalErrorFound = $true
            }
        }

        if ($fatalErrorFound)
        {
            throw "Build Error Detected"
        }
        else
        {
            $transientErrorFound = $true
            Write-Host "Transient Error Detected"
        }
    }

    $jobs = @(Get-Job)
    if ($jobText)
    {
        $jobs = $jobs | ? { $_.Name.Contains($jobText) }
    }

    Write-Host "Dumping thread output"
    foreach ($job in $jobs)
    {
        Write-TeamCityBlockStart "Background Job Output ($($job.Name))"
        Write-Host "================================================================================"
        Write-Host "START: Thread Log  $($job.Name)"
        Write-Host "--------------------------------------------------------------------------------"
        Receive-Job -Job $job -ea SilentlyContinue | Write-Host
        Write-Host "--------------------------------------------------------------------------------"
        Write-Host "END: Thread Log  $($job.Name)"
        Write-Host "================================================================================"
        Write-TeamCityBlockEnd "Background Job Output ($($job.Name))"
    }

    Write-BlockEnd $feedback
    return $transientErrorFound
}

function Restore-NuGetUsingMsBuild
{
    param
    (
        [Parameter(Mandatory = $false)]
        [string] $sln
    )

    Write-BlockStart "Restoring nuget packages for $sln"
    Write-Host (Get-MSBuildLocation $null) /t:restore $sln
    & (Get-MSBuildLocation $null) /t:restore $sln
    if ($LASTEXITCODE -ne 0)
    {
        throw "Errors during nuget restore"
    }
    Write-BlockEnd "Restoring nuget packages for $sln"
}

# Build given makefile using msbuild. Restores NuGet for sln files.
function Invoke-MSBuild
{
    param
    (
        [Parameter(Mandatory = $false)]
        [string] $sln,

        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string] $config = "Release",

        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string] $platform,

        [Parameter(Mandatory = $false)]
        [string] $target,

        [Parameter(Mandatory = $false)]
        [switch] $quiet,

        [Parameter(Mandatory = $false)]
        [switch] $SkipNuGet,

        [Parameter(Mandatory = $false)]
        [string] $UseMSBuildVersion
    )

    if ($target -eq "Publish")
    {
        throw "Use Publish-DotNetCoreProject to publish .Net Core projects"
    }

    if (!$sln)
    {
        $scriptDir = Split-Path -Parent $MyInvocation.PSCommandPath
        Write-Host "Building first solution in $scriptDir"
        $firstSln = Get-ChildItem -Path $scriptDir -Filter *.sln | `
            Select-Object -First 1

        if (!$firstSln)
        {
            throw "No sln found in $scriptDir"
        }

        $sln = $firstSln.FullName
    }

    Write-BlockStart "Building $sln"
    if ($sln.EndsWith(".sln"))
    {
        if (!$SkipNuGet)
        {
            # TODO Need to respect $UseMSBuildVersion here too
            Restore-NuGetPackages $sln
        }

        if ($UseMSBuildVersion)
        {
            $msbuild = Get-MSBuildLocation -msbuildVersion $UseMSBuildVersion
        }
        else
        {
            $msbuild = Get-MSBuildLocation -ForSolution $sln
        }
    }
    else
    {
        $msbuild = Get-MSBuildLocation
    }

    $params = @("/nodereuse:false", "/m", $sln, "/nologo")
    if ($config)
    {
        $params += "/p:Configuration=`"$config`""
    }

    if ($platform)
    {
        $params += "/p:Platform=`"$platform`""
    }

    if ($quiet)
    {
        $params += "/clp:ErrorsOnly"
    }

    if ($target)
    {
        $params += "/t:$target"
    }

    $retriesLeft = 500
    while ($true)
    {
        Write-Progress "Building..."
        Write-Host "$msbuild $params"
        & $msbuild @params | Tee -Variable output
        if ($LASTEXITCODE -ne 0)
        {
            if ((Test-ForTransientErrors $output) -and ($retriesLeft -gt 0))
            {
                Write-Warning "Retrying on transient error"
                $retriesLeft--
                continue
            }

            Test-ForErrors $output
            throw "Build Errors Detected"
        }
        break
    }
    Write-BlockEnd "Building $sln"
}

function Publish-DotNetCoreProject
{
    param
    (
        # Project to publish
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $ProjectPath,

        # Target location to publish to
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $TargetPath,

        # Solution file containing project, for restoring nuget
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]
        $SlnPath,

        # RID to use (see https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)
        [Parameter(Mandatory = $false)]
        [string]
        $RuntimeIdentifier,

        # Some dotnet core builds will fail due to a bug in parallel building
        [Parameter(Mandatory = $false)]
        [switch]
        $SingleThreaded,

        [Parameter(Mandatory = $false)]
        [switch]
        $NoBuild,

        # Don't delete TargetDir before build
        [Parameter(Mandatory = $false)]
        [switch]
        $SkipClean
    )


    if ($SlnPath)
    {
        & dotnet restore $SlnPath
        if ($LASTEXITCODE)
        {
            throw "dotnet restore failed"
        }
    }

    if (!$SkipClean)
    {
        Initialize-Path $TargetPath
    }

    # Due to this bug:
    # https://github.com/dotnet/cli/issues/9514
    # we cannot use --no-restore here
    $args = @(
        "publish"
        $ProjectPath
        "-o"
        $TargetPath
        "-c"
        "Release"
        "/p:Platform=AnyCPU"
        "--self-contained"
        "true"
    )

    if ($NoBuild)
    {
        $args += "--no-build"
    }

    if ($RuntimeIdentifier)
    {
        $args += "-r"
        $args += $RuntimeIdentifier
    }


    if ($SingleThreaded)
    {
        $args += "/m:1"
    }

    $output = ""
    Write-Host dotnet @args
    & dotnet @args 2>&1 | Tee -Variable output

    if (($LASTEXITCODE -ne 0) -and ($output.Contains("error CS0234: The type or namespace name")))
    {
        Write-Host "Publish failed, will try a clean build"
        $output = ""
        & dotnet clean $ProjectPath
        & dotnet @args  2>&1 | Tee -Variable output
    }

    if ($LASTEXITCODE -ne 0)
    {
        Test-ForErrors $output
        throw "dotnet publish failed"
    }
}

function Invoke-SignFile
{
    param
    (
        # File to sign
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $fileToSign,

        # Disable timestamping
        [Parameter(Mandatory = $false)]
        [switch]
        $NoTimestamp
    )

    # Use a selection of TSA authorities, so we don't get throttled for abuse
    $timestampServices = @(
        "http://timestamp.verisign.com/scripts/timstamp.dll"
        "http://timestamp.digicert.com"
        "http://timestamp.comodoca.com"
        "http://sha256timestamp.ws.symantec.com/sha256/timestamp"
        "http://timestamp.globalsign.com/scripts/timstamp.dll"
    )

    $certFile = "$PSScriptRoot\CodeSigningCert.pfx"

    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certFile, 'E1C0d3$ign2019!!')

    $retries = 3
    $blockName = "Signing $fileToSign"
    while ($true)
    {
        $tsa = $null

        try
        {
            Write-BlockStart $blockName
            $signingArgs = @{
                Certificate   = $cert
                FilePath      = $fileToSign
                HashAlgorithm = "Sha256"
            }

            # PowerShell is retarded; argument arrays don't work properly inside background jobs
            # so we have to duplicate this command
            if (!$NoTimestamp)
            {
                $tsa = Get-Random -InputObject $timestampServices
                Write-Host "Sign with timestamp service: $tsa"

                $signingArgs["TimeStampServer"] = $tsa
            }

            Set-AuthenticodeSignature @signingArgs

            Write-Host "Checking signature"
            $auth = Get-AuthenticodeSignature $fileToSign
            if ($auth.Status -ne "Valid")
            {
                throw "Invalid signature in $fileToSign"
            }
            elseif (!$NoTimestamp -and !($auth.TimeStamperCertificate))
            {
                throw "No signature timestamp in $fileToSign"
            }
            Write-Host "Signature OK"

            Write-BlockEnd $blockName

            break
        }
        catch [Exception]
        {
            Write-FullException $_
            Write-Host "Signing failed. Will remove TSA $tsa for this file."
            $timestampServices = $timestampServices | ? { $_ -ne $tsa }
            Write-BlockEnd $blockName
            if ($Retries-- -gt 0)
            {
                Write-Host "Will retry in 5 seconds"
                Start-Sleep -Seconds 5
                continue
            }

            throw
        }
    }
}

function Test-DotNetCoreExists
{
    if (!(Get-Command "dotnet" -ErrorAction SilentlyContinue))
    {
        throw ".Net Core is not installed on this machine (or is not in the PATH)"
    }
}

function Invoke-SignDirectory
{
    param
    (
        # File to sign
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $DirToSign,

        # Disable timestamping
        [Parameter(Mandatory = $false)]
        [switch]
        $NoTimestamp
    )

    Write-BlockStart "Signing $DirToSign"
    Get-ChildItem -Recurse -Path $DirToSign |
    ? { $_.Extension -in (".cab", ".dll", ".exe", ".msi", ".ps1") } |
    ? { $_.Extension -in (".msi", ".cab", ".ps1") -or $_.VersionInfo.CompanyName -imatch "Zeacom" -or $_.VersionInfo.CompanyName -imatch "Enghouse" } |
    % { Invoke-SignFile $_.FullName -NoTimestamp:$NoTimestamp }

    Write-BlockEnd "Signing $DirToSign"
}

function Invoke-SignDirectoryIfInTeamCity
{
    param
    (
        # File to sign
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $DirToSign
    )

    if (!(Test-IsInsideTeamCity))
    {
        Write-Host "Skipping digital signing as not in TeamCity"
        return
    }

    Invoke-SignDirectory $DirToSign
}

function Get-SemanticVersionWithRevision
{
    param
    (
        # Default return value in dev mode
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Default = "1.0.1"
    )

    $version = Get-SemanticVersion
    if ($version.Split(".").Count -lt 4)
    {
        $version = "$version.0"
    }

    return $version
}

function Get-SemanticVersion
{
    param
    (
        # Default return value in dev mode
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Default = "1.0.1"
    )

    Write-Host "Calculating build number"
    $version = Get-BuildNumberInTeamCityBuild $Default
    $branchVersion = getValidBranchVersion ([version] $version).Build
    if ($branchVersion)
    {
        $version = $branchVersion
        Set-TeamCityBuildNumber $version
    }

    return $version
}