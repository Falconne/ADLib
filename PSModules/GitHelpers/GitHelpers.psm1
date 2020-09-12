Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module Logging


function Invoke-Git
{
    Write-Host "git $args"

    $oldErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"

    if ((Get-Host).Name -ne "ConsoleHost")
    {
        Write-Host "Console Host is $((Get-Host).Name)"
        $allArgs = $args -join " "
        # This nonsense in needed because PowerShell remoting munges stderr output into
        # wrapped error objects. This is a problem in background jobs as anything written
        # to stderr causes the task to fail
        & cmd /c "git $allArgs 2>&1"
    }
    else
    {
        & git $args
    }

    $ErrorActionPreference = $oldErrorAction

    if ($LASTEXITCODE) { throw "Command failed with code $LASTEXITCODE : git $args" }
}

function Invoke-Checkout
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $BranchName
    )

    Write-Host "Checking out branch $BranchName..."
    if (Test-LocalBranch $BranchName)
    {
        Invoke-Git checkout $BranchName
    }
    else
    {
        Invoke-Git fetch
        Invoke-Git checkout -t origin/$BranchName
    }

    if ($BranchName -ne (Get-CurrentBranch))
    {
        throw "Could not checkout $BranchName"
    }

    Write-Host "Updating submodules..."
    Invoke-Git submodule update --init --recursive
    Write-Host "Done checking out $BranchName"
}

function Invoke-GitPush
{
    Set-AuthenticatedCurrentRepoUrl
    Write-Host "Invoking quiet git push"
    Invoke-Git push -q @args
}

function Invoke-GitPull
{
    Set-AuthenticatedCurrentRepoUrl
    Write-Host "Invoking quiet git pull"
    Invoke-Git pull -q @args
}

function Set-AuthenticatedCurrentRepoUrl
{
    $url = Invoke-Git config --get remote.origin.url
    $authUrl = (Get-AuthenticatedUrl $url)
    if ($url -ne $authUrl)
    {
        Write-Host "Updating git config to use authenticated URL"
        Invoke-Git remote set-url origin $authUrl
    }
}

function Get-AuthenticatedUrl
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Url
    )

    # TODO Implement if needed

    Write-Host "Getting authenticated URL for $Url"

    if ($Url.Contains("@"))
    {
        # Already authenticated
        return $Url
    }

    if ($Url.StartsWith("https://"))
    {
        $creds = Get-CredentialByName "Name"
        $Url = $Url -replace "//", "//$($creds.username):$($creds.password)@"
    }

    return $Url
}

function Test-RepoClean
{
    return !(git status --porcelain --untracked-files=no)
}

function Test-NoPendingPush
{
    return !(git log --oneline "@{u}..")
}

function Get-CurrentBranch
{
    return (git rev-parse --abbrev-ref HEAD)
}

function Get-CurrentBranchLeafName
{
    return (Get-CurrentBranch).Split("/")[-1]
}

function Test-LocalBranch($branch)
{
    return [bool] (git branch --list $branch)
}

function Test-RemoteBranch($branch)
{
    Set-AuthenticatedCurrentRepoUrl
    $result = [bool] (git ls-remote --heads origin $branch)
    if ($LASTEXITCODE -ne 0)
    {
        throw "Remote check failed"
    }

    return $result
}

function Get-RepoRoot
{
    return (git rev-parse --show-toplevel)
}

function Invoke-CleanBeforeBuild
{
    Write-BlockStart "Running a git clean before build"
    Push-Location (Get-RepoRoot)
    Invoke-Git clean -fX
    Pop-Location
    Write-BlockEnd "Running a git clean before build"

}

Export-ModuleMember -Function *-*
