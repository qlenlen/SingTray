$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $PSScriptRoot "artifacts"
$clientOut = Join-Path $artifacts "client"
$serviceOut = Join-Path $artifacts "service"
$staging = Join-Path $PSScriptRoot "staging"

Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $clientOut | Out-Null
New-Item -ItemType Directory -Force -Path $serviceOut | Out-Null
New-Item -ItemType Directory -Force -Path $staging | Out-Null

dotnet publish (Join-Path $root "SingTray.Client\SingTray.Client.csproj") -c Release -r win-x64 --self-contained true -o $clientOut
dotnet publish (Join-Path $root "SingTray.Service\SingTray.Service.csproj") -c Release -r win-x64 --self-contained true -o $serviceOut

Copy-Item (Join-Path $clientOut "*") $staging -Recurse -Force
Copy-Item (Join-Path $serviceOut "*") $staging -Recurse -Force

Write-Host "Staging output prepared at $staging"
