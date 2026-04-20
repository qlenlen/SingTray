$ErrorActionPreference = "Stop"

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [bool]$SelfContained,

    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$ReleaseRoot = ""
)

$installerRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $installerRoot
$releaseRootPath = if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) { Join-Path $repoRoot "release" } else { $ReleaseRoot }
$variantName = if ($SelfContained) { "self-contained" } else { "framework-dependent" }
$artifactsRoot = Join-Path $installerRoot "artifacts-$variantName"
$stagingRoot = Join-Path $installerRoot "staging-$variantName"
$outputRoot = Join-Path $releaseRootPath $variantName
$outputBaseFileName = "SingTray-$Version-$Runtime-$variantName-setup"

New-Item -ItemType Directory -Force -Path $releaseRootPath | Out-Null
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

& (Join-Path $installerRoot "publish.ps1") `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -SelfContained $SelfContained `
    -ArtifactsRoot $artifactsRoot `
    -StagingRoot $stagingRoot

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    throw "Inno Setup compiler not found: $iscc"
}

& $iscc `
    "/DMyAppVersion=$Version" `
    "/DMyOutputDir=$outputRoot" `
    "/DMyOutputBaseFilename=$outputBaseFileName" `
    (Join-Path $installerRoot "setup.iss")

$setupFile = Join-Path $outputRoot "$outputBaseFileName.exe"
if (-not (Test-Path $setupFile)) {
    throw "Expected installer not found: $setupFile"
}

Write-Host "Built release asset: $setupFile"
