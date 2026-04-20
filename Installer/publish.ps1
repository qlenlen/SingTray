$ErrorActionPreference = "Stop"

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true,
    [string]$ArtifactsRoot = "",
    [string]$StagingRoot = ""
)

$root = Split-Path -Parent $PSScriptRoot
$artifacts = if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) { Join-Path $PSScriptRoot "artifacts" } else { $ArtifactsRoot }
$clientOut = Join-Path $artifacts "client"
$serviceOut = Join-Path $artifacts "service"
$staging = if ([string]::IsNullOrWhiteSpace($StagingRoot)) { Join-Path $PSScriptRoot "staging" } else { $StagingRoot }

Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $clientOut | Out-Null
New-Item -ItemType Directory -Force -Path $serviceOut | Out-Null
New-Item -ItemType Directory -Force -Path $staging | Out-Null

dotnet publish (Join-Path $root "SingTray.Client\SingTray.Client.csproj") -c $Configuration -r $Runtime --self-contained $SelfContained -o $clientOut
dotnet publish (Join-Path $root "SingTray.Service\SingTray.Service.csproj") -c $Configuration -r $Runtime --self-contained $SelfContained -o $serviceOut

Copy-Item (Join-Path $clientOut "*") $staging -Recurse -Force
Copy-Item (Join-Path $serviceOut "*") $staging -Recurse -Force

Write-Host "Staging output prepared at $staging"
