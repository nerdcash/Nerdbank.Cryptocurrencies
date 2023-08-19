#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Builds the rust dynamic link library for all supported targets.
.PARAMETER Release
    Build in release mode.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
Param(
    [switch]$Release
)

$buildArgs = @()
if ($Release) {
    $buildArgs += '-r'
}

Push-Location $PSScriptRoot

$buildArgsNoTargets = $buildArgs
$rustTargets = @(..\..\azure-pipelines\Get-RustTargets.ps1)
$winArm64Required = $false
$rustTargets | % { 
    if ($_ -eq 'aarch64-pc-windows-msvc') {
        $winArm64Required = $true
    }
    else {
        $buildArgs += "--target=$_"
    }
}

if ($winArm64Required) {
    Copy-Item cargoTomlTargets/win-arm64/* -Force
    cargo build @buildArgsNoTargets --target aarch64-pc-windows-msvc
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Copy-Item cargoTomlTargets/others/Cargo.toml -Force
cargo build @buildArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$configPathSegment = 'debug'
if ($Release) { $configPathSegment = 'release' }
$rustTargets | % {
    New-Item -ItemType Directory -Path "..\..\obj\src\nerdbank-zcash-rust\$configPathSegment\$_" -Force | Out-Null
    Copy-Item "target/$_/$configPathSegment/*nerdbank_zcash_rust*" "..\..\obj\src\nerdbank-zcash-rust\$configPathSegment\$_"
}

Pop-Location
