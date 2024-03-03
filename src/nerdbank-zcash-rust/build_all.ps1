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

.\generate_cs_bindings.ps1

$buildArgsNoTargets = $buildArgs
if ($WinArm64Only) {
    $rustTargets = ,'aarch64-pc-windows-msvc'
} elseif ($WinX64Only) {
    $rustTargets = ,'x86_64-pc-windows-msvc'
}else {
    $rustTargets = @(..\..\azure-pipelines\Get-RustTargets.ps1)
}

$rustTargets | % { $buildArgs += "--target=$_"}

Write-Host "Building for $buildArgs"
cargo build @buildArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Pop-Location
