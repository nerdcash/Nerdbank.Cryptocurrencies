#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Builds the rust dynamic link library for all supported targets.
.PARAMETER Release
    Build in release mode.
.PARAMETER WinArm64Only
    Build only the win-arm64 target.
.PARAMETER WinX64Only
    Build only the win-x64 target.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
Param(
    [switch]$Release,
    [switch]$WinArm64Only,
    [switch]$WinX64Only
)

$buildArgs = @()
if ($Release) {
    $buildArgs += '-r'
}

Push-Location $PSScriptRoot

if (!$env:CI) {
    if (!(Test-Path Cargo.toml)) {
        # We have to have *some* Cargo.toml in the root directory, so we'll copy the one for the "others" target.
        Copy-Item cargoTomlTargets/others/* -Force
    }
    .\generate_cs_bindings.ps1
}

$buildArgsNoTargets = $buildArgs
if ($WinArm64Only) {
    $rustTargets = , 'aarch64-pc-windows-msvc'
}
elseif ($WinX64Only) {
    $rustTargets = , 'x86_64-pc-windows-msvc'
}
else {
    $rustTargets = @(..\..\azure-pipelines\Get-RustTargets.ps1)
}

$winArm64Required = $false
$otherTargetsRequired = $false
$rustTargets | % { 
    if ($_ -eq 'aarch64-pc-windows-msvc') {
        $winArm64Required = $true
    }
    else {
        $otherTargetsRequired = $true
        $buildArgs += "--target=$_"
    }
}

if ($winArm64Required) {
    Write-Host "Building for win-arm64"
    Copy-Item cargoTomlTargets/win-arm64/* -Force
    cargo build @buildArgsNoTargets --target aarch64-pc-windows-msvc
    Copy-Item .\Cargo.toml, .\Cargo.lock .\cargoTomlTargets\win-arm64 -Force
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if ($otherTargetsRequired) {
    Write-Host "Building for $buildArgs"
    Copy-Item cargoTomlTargets/others/Cargo.toml -Force
    cargo build @buildArgs
    Copy-Item .\Cargo.toml, .\Cargo.lock .\cargoTomlTargets\others -Force
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$configPathSegment = 'debug'
if ($Release) { $configPathSegment = 'release' }
$rustTargets | % {
    New-Item -ItemType Directory -Path "..\..\obj\src\nerdbank-zcash-rust\$configPathSegment\$_" -Force | Out-Null
    Copy-Item "target/$_/$configPathSegment/*nerdbank_zcash_rust*" "..\..\obj\src\nerdbank-zcash-rust\$configPathSegment\$_"
}

if (!$env:CI -and $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') {
    # We're on a personal machine and we're building for ARM64.
    # Copy the ARM64 Cargo.toml back to the root so that the next manually written `cargo check` will use it.
    copy-item .\cargoTomlTargets\win-arm64\* -force
}

Pop-Location
