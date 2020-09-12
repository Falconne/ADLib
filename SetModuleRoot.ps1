# Dot source this script at the star of your script to automatically
# add the shared modules path to $PSModulePath

$psmodules = "$PSScriptRoot\PSModules"
if $env:PSModulePath -Contains $psmodules
{
    return
}


Write-Host "Loading PS modules from $PSScriptRoot\PSModules"
$env:PSModulePath += ";$PSScriptRoot\PSModules"
