Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module -Force Logging

# Check if PS module exists and if not install it in user scope, then
# imports it into the global scope
function Install-ModuleLocal
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $name
    )

    if (!(Get-Module -ListAvailable -Name $name))
    {
        Write-Host "Installing $name..."
        Enable-PSGallery
        if ($PSVersionTable.PSVersion -lt "5.1")
        {
            Install-Module -Scope CurrentUser $name
        }
        else
        {
            Install-Module -AllowClobber -Scope CurrentUser $name
        }
    }

    Import-Module -Global $name
}

function Save-FileFromNetwork
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $url,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $destination
    )

    [Net.ServicePointManager]::SecurityProtocol = "tls12, tls11, tls"
    $webclient = New-Object System.Net.WebClient
    # On some networks automatic proxy resolution causes several
    # seconds delay
    $webClient.Proxy = $null
    Write-Progress "Downloading $url => $destination"
    $webClient.DownloadFile($url, $destination)
    Write-Host "Done"
}

# Safely delete file or directory and make sure to produce an error
# if it's locked.
function Remove-Path
{
    param
    (
        # Path to delete
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $PathToDelete,

        # Number of times to retry
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [int]
        $Retries = 6,

        # Starting delay in seconds between retries, doubles each try
        [Parameter(Mandatory = $false)]
        [int]
        $RetryDelay = 1
    )

    while ($true)
    {
        try
        {
            if (!(Test-Path $PathToDelete))
            {
                break
            }

            Write-Host "Deleting: $PathToDelete"
            Remove-Item -Force -Recurse $PathToDelete
            if (!(Test-Path $PathToDelete))
            {
                break
            }

            # Not sure is it's PS or NTFS' fault, but deletes are sometimes
            # done asynchronously. So let's wait a bit and see...
            Start-Sleep -Seconds 2

            if (Test-Path $PathToDelete)
            {
                throw "Cannot delete $PathToDelete"
            }

            break
        }
        catch
        {
            Write-Host "Cannot delete $PathToDelete"
            Write-FullException $_
            if ($Retries-- -gt 0)
            {
                Write-Host "Will retry in $RetryDelay seconds, $Retries retries left"
                Start-Sleep -Seconds $RetryDelay
                $RetryDelay *= 2
                Write-Host "Retrying now"
                continue
            }
            Write-Error "Cannot delete $PathToDelete"
            throw
        }
    }
}

# Helper function to delete and recreate a path, for a clean start
function Initialize-Path
{
    param
    (
        # Path to initialize
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path
    )

    Remove-Path $Path
    mkdir $Path | Out-Null
}

# Creates a directory if it doesn't exist
function New-Directory
{
    param
    (
        # Directory to create
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path
    )

    if (Test-Path $Path)
    {
        return
    }

    Write-Host "Creating directory $Path"
    mkdir $Path | Out-Null
}

function Find-FirstFileUnder
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $root,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $name
    )

    $result = Get-ChildItem -Recurse -Path $root -Filter $name | Select-Object -First 1
    if (!$result)
    {
        throw "$name not found anywhere under $root"
    }

    return $result.FullName
}

# Does a mirrored robocopy with error checking
function Copy-WithMirror
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [IO.DirectoryInfo] $src,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [IO.DirectoryInfo] $dst
    )

    if (!(Test-Path $src)) { throw "Directory not found: $src" }
    Write-BlockStart "Mirroring $src to $dst"
    New-Directory $dst
    & robocopy $src $dst /MIR /MT /R:3
    if ($LASTEXITCODE -gt 4)
    {
        throw "Error while mirroring: $LASTEXITCODE"
    }
    Write-BlockEnd "Mirroring $src to $dst"

}

# Does a non-mirrored robocopy with error checking
function Copy-WithoutMirror
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [IO.DirectoryInfo] $src,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [IO.DirectoryInfo] $dst
    )

    if (!(Test-Path $src)) { throw "Directory not found: $src" }
    Write-BlockStart "Copying $src to $dst"
    New-Directory $dst
    & robocopy $src $dst /E /MT /R:3
    if ($LASTEXITCODE -gt 3)
    {
        throw "Error while robocopying: $LASTEXITCODE"
    }
    Write-BlockEnd "Copying $src to $dst"
}

function Get-FileAttribute
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $root,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $name
    )
}

function Test-EnvVarTrue
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $name
    )

    try
    {
        $value = [environment]::GetEnvironmentVariable($name, "Process")
        if ([string]::IsNullOrWhiteSpace($value))
        {
            return $false
        }

        return ($value -ne 0)
    }
    catch [Exception]
    {
        return $false
    }
}

# Returns true if given file contains the given string
function Test-FileContainsString
{
    param
    (
        # Path to file to search
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $File,

        # Text to search for
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $SearchText
    )

    return [bool] (Get-Content $File | Select-String $SearchText)
}

# Removes the \\?\ prefix from Item paths. Filter results from Get-ChildItem -LiteralPath
# cmdlet (used to avoid Windows path length limit).
function Get-DeunicodedItem
{
    param
    (
        # File object
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [ValidateNotNullOrEmpty()]
        [IO.FileInfo]
        $Item
    )

    Process
    {
        $path = $Item.FullName
        $path = $path.Replace("\\?\", "")
        return (Get-Item $path)
    }
}

function Get-MD5Sum($string)
{
    $md5 = New-Object -TypeName System.Security.Cryptography.MD5CryptoServiceProvider
    $utf8 = New-Object -TypeName System.Text.UTF8Encoding
    return [System.BitConverter]::ToString($md5.ComputeHash($utf8.GetBytes($string)))
}

function Get-StableGUIDForString($string)
{
    $hash = getMD5Sum($string)
    $hash = $hash -Replace "-", ""
    return New-Object -TypeName System.Guid($string)
}

function getSevenZip
{
    $sevenZip = "$PSScriptRoot\..\..\7z.exe"
    if (Test-Path)
    {
        throw "$sevenZip not found"
    }

    return $sevenZip
}

# Creates a standard Windows zip file out of a directory
function New-StandardZip
{
    param
    (
        # Directory to zip
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Source,

        # Target zip file location
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Target
    )

    if (!(Test-Path $Source))
    {
        throw "$Source not found"
    }

    Remove-Path $Target
    $sevenZip = getSevenZip
    Write-Host "Zipping $Source into $Target"
    Write-Host $sevenZip a "$Target" "$Source\*"
    & $sevenZip a "$Target" "$Source\*"
    if ($LASTEXITCODE -ne 0)
    {
        throw "7zip command failed"
    }
}

function Expand-StandardZip
{
    param
    (
        # Zip file
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Source,

        # Directory to extract to
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Target
    )

    $sevenZip = getSevenZip
    Write-Host "Unzipping $Source to $Target"
    Write-Host "$sevenZip x $Source -o$Target -y"
    & $sevenZip x $Source "-o$Target" -y
    if ($LASTEXITCODE -ne 0)
    {
        throw "7zip command failed"
    }
}

# Removes the \\?\ prefix from directory item paths. Filter results from Get-ChildItem -LiteralPath
# cmdlet (used to avoid Windows path length limit).
function Get-DeunicodedDirectoryItem
{
    param
    (
        # File object
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [ValidateNotNullOrEmpty()]
        [IO.DirectoryInfo]
        $Item
    )

    Process
    {
        $path = $Item.FullName
        $path = $path.Replace("\\?\", "")
        return (Get-Item $path)
    }
}

# Display an error message in red font and exit with code 1
function Stop-WithError
{
    param
    (
        # Message to show
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Message
    )

    Write-CustomError $Message
    exit 1
}


# Runs given batch file, then propagates the env vars set by said
# file in current PowerShell scope.
function Get-BatchFile
{
    param
    (
        # Batch file to invoke
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $File
    )

    $cmd = "`"$File`" && set"
    cmd /c $cmd | % {
        $p, $v = $_.split('=')
        if ($p)
        {
            Set-Item -path Env:$p -value $v
        }
    }
}

# Read content from a file and retry if that fails due to the file being in use
function Get-ContentSafely
{
    param
    (
        # File to read
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path
    )

    $retryAttempts = 30
    foreach ($i in 1..$retryAttempts)
    {
        if (!(Test-Path $Path))
        {
            throw "Get-ContentSafely: $Path not found"
        }

        try
        {
            $content = Get-Content $Path
            return $content
        }
        catch
        {
            if ($i -ge $retryAttempts)
            {
                throw
            }

            Write-Host $_.Exception.Message
            Write-Host "Get-ContentSafely: Error reading from $Path, will retry"
            Start-Sleep -Seconds 1
        }
    }

    throw
}

# Write content to a file and retry if that fails due to the file being in use
function Write-LinesToFileSafely
{
    param
    (
        # File to read
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path,

        # Array of lines to write
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        $Content
    )

    $retryAttempts = 30
    foreach ($i in 1..$retryAttempts)
    {
        try
        {
            Set-Content -Encoding UTF8 $Path $Content
            break
        }
        catch
        {
            if ($i -ge $retryAttempts)
            {
                throw
            }

            Write-Host $_.Exception.Message
            Write-Host "Write-LinesToFileSafely: Error writing to $Path, will retry"
            Start-Sleep -Seconds 1
        }
    }
}

# Show exception location, unwrapping it from a remote exception if needed
function Get-ExceptionInvocationInfo ($ex)
{
    if ($ex.Exception -is [System.Management.Automation.RemoteException])
    {
        return $ex.Exception.SerializedRemoteInvocationInfo.PositionMessage
    }

    return $ex.InvocationInfo.PositionMessage
}

# Show exception message, unwrapping it from a remote exception if needed
function Get-Exception ($ex)
{
    if ($ex.Exception -is [System.Management.Automation.RemoteException])
    {
        return $ex.Exception.SerializedRemoteException
    }

    return $ex.Exception
}

function Get-SecureCredential
{
    param
    (
        # Username with domain if applicable
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Username,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Password
    )

    $pass = ConvertTo-SecureString -AsPlainText $Password -Force
    return New-Object System.Management.Automation.PSCredential -ArgumentList $Username, $pass
}

function Test-ObjectHasProperty
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        $Object,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Property
    )

    if (!$Object)
    {
        return $false
    }

    return $Object.PSobject.Properties.Name -contains $Property
    # return [bool]($object.PSobject.Properties.name -match $PropertyName)
}

function Connect-ToUNCPath
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Username,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Password,

        # Force login before testing path, for shares that have public read-only
        # access but private write access`
        [Parameter(Mandatory = $false)]
        [switch]
        $ForceLogin
    )

    Write-Host "Checking access to $Path"
    if (!$ForceLogin)
    {
        try
        {
            if (Test-Path $Path)
            {
                Write-Host "Path OK"
                return
            }
        }
        catch
        {
            Write-Host "Cannot access $Path, will try credentials"
        }
    }

    Write-Progress "Logging in as $Username"
    & net.exe use $Path /user:$Username $Password | Out-Host

    $retries = 3
    while (--$retries -ge 0)
    {
        if (Test-Path $Path)
        {
            return
        }

        if ($retries -gt 0)
        {
            Write-Host "Will retry access"
            Start-Sleep 10
        }
    }
    throw "Unable to access $Path"
}

# Find the version of VS/MSBuild to use for given sln file
function Get-SolutionVersion
{
    param
    (
        # Path to sln file
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path
    )

    Write-Host "Checking version of $Path"
    $slnContent = Get-Content $Path
    foreach ($line in $slnContent)
    {
        if ($line -Match 'VisualStudioVersion = ([\d]+\.[\d]+)')
        {
            $msbuildVersion = $matches[1]
            Write-Host "Solution is using MSBuild v$msbuildVersion"
            return $msbuildVersion
        }
    }

    throw "No VisualStudioVersion in $PATH"
}


function Get-VSWhere
{
    $vswhereLocation = Get-NuGetToolPackageDownloadLocation "vswhere"
    if (!(Test-Path $vswhereLocation))
    {
        $vswhereLocation = Get-LatestNuGetToolPackage "vswhere"
    }
    $vswhere = Join-Path $vswhereLocation "vswhere.exe"

    return $vswhere
}

function Get-MSBuildLocation
{
    param
    (
        [Parameter(Mandatory = $false)]
        [string] $msbuildVersion,

        [Parameter(Mandatory = $false)]
        [string]
        $ForSolution
    )

    Write-Host "Finding MSBuild"

    $vswhere = Get-VSWhere
    $params = @("-latest", "-requires", "Microsoft.Component.MSBuild", "-property", "installationPath", "-products", "*")

    if ($ForSolution)
    {
        Write-Host "For $ForSolution"

        $startVersion = [version] (Get-SolutionVersion $ForSolution)
        $endVersion = "$($startVersion.Major + 1).0"
        $msbuildVersion = "[$startVersion, $endVersion)"
    }
    elseif ($msbuildVersion)
    {
        Write-Host "Looking for explicit MSBuild version $msbuildVersion"
    }
    else
    {
        Write-Host "Finding latest available version"
    }

    if (!([string]::IsNullOrWhiteSpace($msbuildVersion)))
    {
        $params += "-version", $msbuildVersion
    }

    $installationPath = & $vswhere @params
    if (!$installationPath)
    {
        Write-Error "MSBuild version $msbuildVersion not found"
        exit 1
    }

    $msbuild = "$installationPath\MSBuild\Current\Bin\MSBuild.exe"
    if (!(Test-Path $msbuild))
    {
        $msbuild = "$installationPath\MSBuild\15.0\Bin\MSBuild.exe"
    }

    if (!(Test-Path $msbuild))
    {
        Write-Host "msbuild not found at $msbuild"
    }

    Write-Host "Using msbuild from: $msbuild"
    return $msbuild
}


Export-ModuleMember -Function *-*
