Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module TeamCity

# Display a friendly progress message
function Write-CustomProgress
{
    param
    (
        # Message to show
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Message
    )

    if (Test-IsInsideTeamCity)
    {
        Write-TeamCityProgress $Message
    }
    else
    {
        Write-Host -ForegroundColor Green $Message
    }
}

# Display an alarming warning message
function Write-Warning
{
    param
    (
        # Message to show
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Message
    )

    if (Test-IsInsideTeamCity)
    {
        Write-TeamCityMessage -Text $Message -Status "WARNING"
    }
    else
    {
        Write-Host -ForegroundColor Yellow $Message
    }
}

# Display a red error
function Write-CustomError
{
    param
    (
        # Message to show
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Message
    )

    if (Test-IsInsideTeamCity)
    {
        Write-TeamCityMessage -Text $Message -Status "ERROR"
    }
    else
    {
        Write-Host -ForegroundColor Red $Message
    }
}

# Start a distinct block of logging. Inside TeamCity this will use TeamCity
# collapsible blocks while the console will uses line markers
function Write-BlockStart
{
    param
    (
        # Block name / title
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $BlockName
    )

    if (Test-IsInsideTeamCity)
    {
        Write-TeamCityBlockStart $BlockName
    }
    else
    {
        Write-Host " "
        Write-MajorLineMarker
        Write-CustomProgress $BlockName
        Write-MinorLineMarker
    }
}

function Write-BlockEnd
{
    param
    (
        # Block name / title
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $BlockName
    )

    if (Test-IsInsideTeamCity)
    {
        Write-TeamCityBlockEnd $BlockName
    }
    else
    {
        Write-MajorLineMarker
    }
}

function Write-MajorLineMarker
{
    Write-Host "================================================================================`n"
}

function Write-MinorLineMarker
{
    Write-Host "--------------------------------------------------------------------------------"
}


function getExceptionInvocationInfo ($ex)
{
    try
    {
        if ($ex.Exception -is [System.Management.Automation.RemoteException])
        {
            return $ex.Exception.SerializedRemoteInvocationInfo.PositionMessage
        }
    }
    catch
    {

    }

    try
    {
        return $ex.InvocationInfo.PositionMessage
    }
    catch
    {
        return "No InvocationInfo for Exception"
    }
}

# Check if exception came from PS remoting and if so, unwrap it.
function getException ($ex)
{
    try
    {
        if ($ex.Exception -is [System.Management.Automation.RemoteException])
        {
            return $ex.Exception.SerializedRemoteException
        }
    }
    catch
    {

    }

    return $ex.Exception
}

function Write-FullException($ex)
{
    Write-MajorLineMarker
    Write-Host "Exception thrown:"
    Write-MinorLineMarker
    $ex | Select-Object * | Out-Host
    Write-MinorLineMarker
    getException $ex | Select-Object * | Out-Host
    Write-MinorLineMarker
    getExceptionInvocationInfo $ex | Select-Object * | Out-Host
    Write-MajorLineMarker
}

Export-ModuleMember -Function *-*
