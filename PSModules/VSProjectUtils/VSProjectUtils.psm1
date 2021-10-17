Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-IsSDKStyleProject($csproj)
{
    return [bool] (Get-Content $csproj | Select-String -pattern "<Project [^>]*SDK")
}

function Test-IsExecutableProject($csproj)
{
    return [bool] (Get-Content $csproj | Select-String -pattern "<OutputType>Exe")
}

function Get-ReferencedProjectsIn
{
    param
    (
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [IO.FileInfo] $project
    )

    if ($project.GetType().Name -eq "String")
    {
        $project = Get-Item $project
    }

    $baseDirectory = $project.Directory.FullName
    $refLines = Get-Content $project.FullName | ? { $_.Contains("<ProjectReference Include=") }
    $refProjects = @()
    foreach ($line in $refLines)
    {
        if (!($line -Match '"(.+)"')) { continue }
        $file = $matches[1]
        $file = "$baseDirectory\$file"
        if (!(Test-Path $file)) { throw "Project $files, referenced from $($project.FullName) was not found" }
        $refProjects += (Get-Item $file)
    }

    return $refProjects
}

Export-ModuleMember -Function *-*
