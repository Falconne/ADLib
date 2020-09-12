Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Import-Module ResourceResolver

function _TeamCityFormatMessage
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Message
    )

    $formatted = $Message
    $formatted = $formatted -replace "'", "|'"
    $formatted = $formatted -replace "\r", "|r"
    $formatted = $formatted -replace "\n", "|n"
    $formatted = $formatted -replace "[\[]", "|["
    $formatted = $formatted -replace "]", "|]"
    return $formatted
}

<#
 .Synopsis
  Writes an exception notification to the TeamCity log.

 .Description
  Formats the given error object and sends it to the TeamCity build log as a message with status = ERROR.
  Depending on the configuration of the build step, this might result in the build to fail.

 .Parameter Exception
  The exception to report.

 .Example
  Write-TeamCityException New-Object Exception "My test error."
#>
function Write-TeamCityException
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNull()]
        [System.Management.Automation.ErrorRecord] $Exception
    )

    $errorName = $Exception.FullyQualifiedErrorId
    $errorMessage = "Error:"
    $errorMessage = $errorMessage + [Environment]::NewLine + $Exception.Exception.ToString()
    $errorMessage = $errorMessage + [Environment]::NewLine + "Target Object:"
    $errorMessage = $errorMessage + [Environment]::NewLine + $Exception.TargetObject
    $errorMessage = $errorMessage + [Environment]::NewLine + "Invocation Info:"
    $errorMessage = $errorMessage + [Environment]::NewLine + $Exception.InvocationInfo.Line
    $errorMessage = $errorMessage + [Environment]::NewLine + $Exception.InvocationInfo.PositionMessage

    Write-TeamCityMessage -Text "Error encountered in PowerShell script: '$errorName'." -ErrorDetails $errorMessage -Status "ERROR"
}

<#
 .Synopsis
  Writes a message to the TeamCity log.

 .Description
  Writes the given message into the TeamCity build log. Additionally, if there is an error, the details for the error may be provided.

 .Parameter Text
  The message text.

 .Parameter Status
  The message severity.

 .Parameter ErrorDetails
  The error details. TeamCity ignores this if the status is not ERROR.

 .Example
  Write-TeamCityMessage -Text "All is good."

 .Example
  Write-TeamCityMessage -Text "All is not so good." -Status WARNING

 .Example
  Write-TeamCityMessage -Text "All is terrible." -ErrorDetails "My dog ran away." -Status ERROR
#>
function Write-TeamCityMessage
{
    param
    (
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string] $Text,

        [Parameter(Mandatory = $false)]
        [ValidateSet("NORMAL", "WARNING", "FAILURE", "ERROR")]
        [string] $Status = "NORMAL",

        [Parameter(Mandatory = $false)]
        [string] $ErrorDetails
    )

    $formattedErrorDetails = ""
    $formattedText = _TeamCityFormatMessage $Text
    if ($ErrorDetails) { $formattedErrorDetails = _TeamCityFormatMessage $ErrorDetails }
    Write-Host "##teamcity[message text='$formattedText' errorDetails='$formattedErrorDetails' status='$Status']"
}

<#
 .Synopsis
  Writes a progress notification message to the TeamCity log.

 .Description
  The message will be shown on the projects dashboard for corresponding build and on the build results page.

 .Parameter Message
  The progress notification to report.

 .Example
  Write-TeamCityProgress "Flurging the wurble."
#>
function Write-TeamCityProgress
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Message
    )

    $messageFormatted = _TeamCityFormatMessage $Message
    Write-Host "##teamcity[progressMessage '$messageFormatted']"
}

<#
 .Synopsis
  Begins an indentation block in the TeamCity log.

 .Description
  Starts a collapsible section in the TeamCity build log.
  Within the block, all build log lines are prefixed with the block's name and indented.

 .Parameter BlockName
  The name of the block. This name must match the value provided in the Write-TeamCityBlockEnd function.

 .Example
  Write-TeamCityBlockStart "My block"
#>
function Write-TeamCityBlockStart
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $BlockName
    )

    $formattedText = _TeamCityFormatMessage $BlockName
    Write-Host "##teamcity[blockOpened name='$formattedText']"
    Write-TeamCityProgress $BlockName
}

<#
 .Synopsis
  Ends an indentation block in the TeamCity log.

 .Description
  Ends a collapsible section in the TeamCity build log. The $BlockName must exactly match the name which has been
  used to start the block.
  Within the block, all build log lines are prefixed with the block's name and indented.

 .Parameter BlockName
  The name of the block. This name must match the value provided in the Write-TeamCityBlockStart function.

 .Example
  Write-TeamCityBlockEnd "My block"
#>
function Write-TeamCityBlockEnd
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $BlockName
    )

    $formattedText = _TeamCityFormatMessage $BlockName
    Write-Host "##teamcity[blockClosed name='$formattedText']"
}

<#
 .Synopsis
  Begins a collapsible progress region in the TeamCity log.

 .Description
  Starts a collapsible region in the TeamCity build log.
  The messages within this region are neither pre-fixed nor indented.

 .Parameter ProgressDescription
  The progress description to report.

 .Example
  Write-TeamCityProgressBegin "My progress"
#>
function Write-TeamCityProgressBegin
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $ProgressDescription
    )
    Write-Host "##teamcity[progressStart '$ProgressDescription']"
}

<#
 .Synopsis
  Ends a collapsible progress region in the TeamCity log.

 .Description
  Ends a collapsible region in the TeamCity build log.
  The messages within this region are neither pre-fixed nor indented.

 .Parameter ProgressDescription
  The progress description to report. This must match the description provided in the Write-TeamCityProgressBegin function.

 .Example
  Write-TeamCityProgressEnd "My progress"
#>
function Write-TeamCityProgressEnd
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $ProgressDescription
    )
    Write-Host "##teamcity[progressFinish '$ProgressDescription']"
}

function _GetTeamCityBuildDataXml
{
    param
    (
        [string] $TeamCityServerUrl,
        [int] $BuildId
    )

    # Get build info
    [UriBuilder] $tcUriBuilder = New-Object UriBuilder $TeamCityServerUrl
    $tcUriBuilder.Path = "guestAuth/app/rest/builds/id:$BuildId"
    $url = $tcUriBuilder.ToString()
    [Xml] $xml = (New-Object System.Net.WebClient).DownloadString($url)

    return $xml
}

<#
 .Synopsis
  Determines the URLs for build artifact dependencies of the specified build.

 .Description
  Communicates with TeamCity to determine the URLs of the artifact dependencies for a particular build
  and returns them to the pipeline.

 .Parameter $TeamCityServerUrl
  The location of the TeamCity server; e.g., http://build-server:1234/.

 .Parameter $BuildId
  The TeamCity build identifier for the build in question.

 .Example
  Get-TeamCityBuildArtifactDependencyUrls -TeamCityServerUrl "http://akl-tc01-2k8" -BuildId 53374
#>
function Get-TeamCityBuildArtifactDependencyUrls
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $TeamCityServerUrl,

        [Parameter(Mandatory = $true)]
        [int] $BuildId
    )

    # Get build info
    [Xml] $xml = _GetTeamCityBuildDataXml $TeamCityServerUrl $BuildId

    # Parse the artifact URLs
    $dependencies = $xml.SelectNodes("./build/artifact-dependencies/build[@status='SUCCESS']")

    $dependencies | % {
        [UriBuilder] $builder = New-Object UriBuilder $TeamCityServerUrl
        $builder.Path = "guestAuth/repository/downloadAll/{0}/{1}:id" -f $_.buildTypeId, $_.id

        Write-Output $builder.ToString()
    }
}

<#
 .Synopsis
  Determines the URL for a specific build artifact dependency of the specified build.

 .Description
  Communicates with TeamCity to determine the URL of an artifact dependency for a particular build
  and return it to the pipeline.

 .Parameter $TeamCityServerUrl
  The location of the TeamCity server; e.g., http://build-server:1234/.

 .Parameter $BuildId
  The TeamCity build identifier for the build in question.

 .Parameter $ArtifactBuildTypeId
  The TeamCity build type identifier for the artifact dependency.

 .Example
  Get-TeamCityBuildArtifactDependencyUrl -TeamCityServerUrl "http://akl-tc01-2k8" -BuildId 55822 -ArtifactBuildTypeId bt1040
#>
function Get-TeamCityBuildArtifactDependencyUrl
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $TeamCityServerUrl,

        [Parameter(Mandatory = $true)]
        [int] $BuildId,

        [Parameter(Mandatory = $true)]
        [string] $ArtifactBuildTypeId
    )

    # Get build info
    [Xml] $xml = _GetTeamCityBuildDataXml $TeamCityServerUrl $BuildId

    # Parse the artifact URLs
    $dependency = $xml.SelectSingleNode("./build/artifact-dependencies/build[@status='SUCCESS' and @buildTypeId='$ArtifactBuildTypeId']")

    [UriBuilder] $builder = New-Object UriBuilder $TeamCityServerUrl
    $builder.Path = "guestAuth/repository/downloadAll/{0}/{1}:id" -f $ArtifactBuildTypeId, $dependency.id

    return $builder.ToString()
}

<#
 .Synopsis
  Determines the TeamCity build ID of the last successful build of a specified type.

 .Description
  Communicates with TeamCity to determine the build identifier of the last successful build of a specific build type.

 .Parameter $TeamCityServerUrl
  The location of the TeamCity server; e.g., http://build-server:1234/.

 .Parameter $BuildTypeId
  The TeamCity build type identifier for the build.

 .Example
  Get-TeamCityBuildIdForLastSuccessfulBuild -TeamCityServerUrl "http://akl-tc01-2k8" -BuildTypeId bt1040
#>
function Get-TeamCityBuildIdForLastSuccessfulBuild
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $TeamCityServerUrl,

        [Parameter(Mandatory = $true)]
        [string] $BuildTypeId
    )

    [UriBuilder] $tcUriBuilder = New-Object UriBuilder $TeamCityServerUrl
    $tcUriBuilder.Path = "guestAuth/app/rest/buildTypes/$BuildTypeId/builds/status:success/id"
    $url = $tcUriBuilder.ToString()
    [string] $buildIdText = (New-Object System.Net.WebClient).DownloadString($url)

    if ($buildIdText)
    {
        [int] $buildId = $buildIdText
        return $buildId
    }

    return 0
}

<#
 .Synopsis
  Determines the URL for the specified build artifact of the specified build.

 .Description
  Creates the the URL for the specified build artifact of the specified build
  and returns them to the pipeline.

 .Parameter $TeamCityServerUrl
  The location of the TeamCity server; e.g., http://build-server:1234/.

 .Parameter $BuildId
  The TeamCity build identifier for the build in question.

 .Parameter $ArtifactZipFile
  The name of the artifact zip file.

 .Example
  Get-TeamCityBuildArtifactUrl -TeamCityServerUrl "http://akl-tc01-2k8" -BuildId 53374 -Artifact 'MyArtifact.zip'
#>
function Get-TeamCityBuildArtifactUrl
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $TeamCityServerUrl,

        [Parameter(Mandatory = $true)]
        [string] $BuildId,

        [Parameter(Mandatory = $true)]
        [string] $Artifact
    )

    [UriBuilder] $builder = New-Object UriBuilder $TeamCityServerUrl
    $builder.Path = "guestAuth/app/rest/builds/id:" + $BuildId + "/artifacts/files/" + $Artifact
    Write-Output $builder.ToString()
}

<#
 .Synopsis
  Notifies TeamCity to gather build artifacts.

 .Description
  Writes a message to the build log to signal to TeamCity that files should be gathered as artifacts.

 .Parameter ArtifactPath
  The path to the artifact file that should be gathered.

 .Example
  Publish-TeamCityArtifact "C:\Results\TestResults.log"
#>
function Publish-TeamCityArtifact
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $ArtifactPath
    )

    process
    {
        Write-Host "##teamcity[publishArtifacts '$ArtifactPath']"
    }
}

<#
 .Synopsis
  Publishes XML results to TeamCity.

 .Description
  Publishes an XML file containing test results or other information that TeamCity understands.

 .Parameter Path
  The path to the XML file to process.

 .Parameter Type
  The type of XML data to publish.

 .Example
  Publish-TeamCityXmlResults -Type nunit -Path TestResult.xml
#>
function Publish-TeamCityXmlResults
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [ValidateSet("nunit", "mstest")] # Add others as required...
        [string] $Type
    )

    process
    {
        Write-Host "##teamcity[importData type='$Type' path='$Path']"
    }
}

<#
 .Synopsis
  Writes a build status message to the TeamCity log.

 .Description
  Writes a build status notification to the TeamCity build log. This can optionally include

 .Parameter Status
  The build status. Can be FAILURE or SUCCESS.

 .Parameter Text
  Optional additional text to display in the build status message.

 .Example
  Write-TeamCityBuildStatus -Status "SUCCESS" -Text "Everything worked"
#>
function Write-TeamCityBuildStatus
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateSet("FAILURE", "SUCCESS")]
        [string] $Status,

        [string] $Text
    )

    [string] $optionalTextAttribute = $null
    if ($Text)
    {
        $formattedText = _TeamCityFormatMessage ("; " + $Text)
        $optionalTextAttribute = " text='{build.status.text}$formattedText'"
    }

    Write-Host "##teamcity[buildStatus status='$Status'$optionalTextAttribute]"
}


<#
 .Synopsis
  Notifies TeamCity about a test being started.

 .Description
  Notifies TeamCity about a test being started.
  Tests are reported as succeeded/failed etc.

 .Parameter TestName
  The name of the test.

 .Example
  Write-TeamCityTestStart "My test"
#>
function Write-TeamCityTestStart
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $TestName
    )
    $TestName = _TeamCityFormatMessage $TestName
    Write-Host "##teamcity[testStarted name='$TestName']"
}

<#
 .Synopsis
  Notifies TeamCity about a test being finished.

 .Description
  Notifies TeamCity about a test being started. The $TestName must exactly match the name which has been
  used to start the test.
  Tests are reported as succeeded/failed etc.

 .Parameter TestName
  The name of the test. This name must match the value provided in the Write-TeamCityTestStart function.

 .Example
  Write-TeamCityTestEnd "My test"
#>
function Write-TeamCityTestEnd
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $TestName
    )

    $TestName = _TeamCityFormatMessage $TestName
    Write-Host "##teamcity[testFinished name='$TestName']"
}

<#
 .Synopsis
  Notifies TeamCity about a failed test.

 .Description
  Notifies TeamCity about a failed test. The $TestName must exactly match the name which has been
  used to start the test.

 .Parameter TestName
  The name of the test. This name must match the value provided in the Write-TeamCityTestStart function.

 .Example
  Write-Host "##teamcity[testFailed name='My test' message='Installation Failed' details='See Install Log']"
#>
function Write-TeamCityTestFailed
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $TestName,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Message,

        [Parameter(Mandatory = $false)]
        [string]
        $Details
    )

    $TestName = _TeamCityFormatMessage $TestName
    $formattedMessage = _TeamCityFormatMessage $Message
    $formattedDetails = _TeamCityFormatMessage $Details
    Write-Host "##teamcity[testFailed name='$TestName' message='$formattedMessage' details='$formattedDetails']"
}

function Set-TeamCityBuildNumber
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $BuildNumber
    )

    Write-Host "Setting TeamCity build number to $BuildNumber"
    Write-Host "##teamcity[buildNumber  '$BuildNumber']"
}

function Set-TeamCityParameter
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $name,

        [Parameter(Mandatory = $false)]
        [string] $value = ""
    )

    Write-Host "##teamcity[setParameter name='$name' value='$value']"
}

# Get REST access details with automation user credentials
function Get-AccessDetails
{
    param
    (
        # Use automation user with admin access
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [switch]
        $WithAdminAccess
    )

    throw "Access details not set up"
    $user = "user"
    $password = 'password'

    $secpasswd = ConvertTo-SecureString $password -AsPlainText -Force
    $credentials = New-Object System.Management.Automation.PSCredential($user, $secpasswd)

    $url = (Get-ResourceValue "teamCityUrl")
    return @{
        "URL"         = $url;
        "Credentials" = $credentials
    }
}

function Invoke-TeamCityMethod
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Query,

        [Parameter(Mandatory = $false)]
        [string]
        $Method = "GET",

        # Use automation user with admin access
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [switch]
        $WithAdminAccess
    )

    $accessDetails = Get-AccessDetails -WithAdminAccess:$WithAdminAccess
    $url = "$($accessDetails['URL'])/httpAuth/app/rest/$Query"

    Write-Host "Invoke: $url"
    return Invoke-RestMethod -Uri $url -Method $Method -Credential $accessDetails['Credentials']
}

function Invoke-PinBuild
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $BuildId,

        [Parameter(Mandatory = $false)]
        [string] $Comment = ""
    )

    $accessDetails = Get-AccessDetails
    $url = "$($accessDetails['URL'])/httpAuth/app/rest/builds/id:$BuildId/pin/"

    Write-Host "Pinning build $buildId"
    Invoke-RestMethod -Uri $url -Method Put -Credential $accessDetails['Credentials'] `
        -Body $Comment
}

function Invoke-AddTagToBuild
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $BuildId,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name
    )

    $postData = "<tags count=`"1`"><tag name=`"$Name`"></tag></tags>"

    $accessDetails = Get-AccessDetails
    $url = "$($accessDetails['URL'])/httpAuth/app/rest/builds/id:$BuildId/tags/"

    Write-Host "Adding tag to build, posting"
    Write-Host $postData
    Write-Host "To URL: $url";
    Invoke-RestMethod -Uri $url -Method Post -Credential $accessDetails['Credentials'] `
        -ContentType "application/xml" -Body $postData
}

function Invoke-TeamCityBuild
{
    param
    (
        [Parameter(Mandatory = $true, HelpMessage = "Build configuration ID")]
        [string] $configId,

        [Parameter(Mandatory = $false, HelpMessage = "Properties")]
        $properties,

        [Parameter(Mandatory = $false, HelpMessage = "Branch")]
        [string] $branch
    )

    $branchProperty = ""
    if ($branch)
    {
        $branchProperty = "branchName=`"$branch`""
    }

    $propertiesAsXML = ""
    if ($properties)
    {
        foreach ($propertyName in $properties.Keys)
        {
            $propertiesAsXML += "<property name=`"$propertyName`" value=`"$($properties[$propertyName])`"/>"
        }
    }

    $postData = @"
        <build $branchProperty>
            <buildType id="$configId"/>
            <properties>
            $propertiesAsXML
            </properties>
        </build>
"@

    $accessDetails = Get-AccessDetails
    $url = "$($accessDetails['URL'])/httpAuth/app/rest/buildQueue"

    Write-Host "Invoking TeamCity build configuration $configId"
    Invoke-RestMethod -Uri $url -Method Post -Credential $accessDetails['Credentials'] -ContentType "application/xml" `
        -Body $postData
}


# Returns true if we are inside a TeamCity build
function Test-IsInsideTeamCity
{
    return (Test-Path Env:\BUILD_NUMBER)
}

function Get-BuildProperties
{
    $propsFile = $Env:TEAMCITY_BUILD_PROPERTIES_FILE
    Write-Host "Reading TeamCity porperties from $propsFile"
    return ConvertFrom-StringData (Get-Content $propsFile -raw)
}

function Get-BuildProperty
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name
    )

    $props = Get-BuildProperties
    $value = $props[$Name]
    Write-Host "TeamCity property $Name => $value"
    return $value
}


function Get-CurrentBuildId
{
    $props = Get-BuildProperties
    $buildId = $props["teamcity.agent.dotnet.build_id"]
    Write-Host "Build ID is $buildId"
    return $buildId
}

# Gets build number from BUILD_NUMBER environment variable that should be set
# during a TeamCity build. Returns $Default if not running inside TeamCity.
function Get-BuildNumberInTeamCityBuild
{
    param
    (
        # Default return value in dev mode
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Default = "1.0.0.1"
    )
    $version = $Env:BUILD_NUMBER
    if (!$version)
    {
        Write-Host "Warning: %BUILD_NUMBER% not set, assuming not in TeamCity"
        $version = $Default
    }

    Write-Host "Using build number $version"

    return $version
}

Export-ModuleMember -Function *-*
