#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Builds the rust dynamic link library for all supported targets.
.PARAMETER Release
    Build in release mode.
.PARAMETER Locked
    Specify --locked to the cargo build command.
.PARAMETER WinArm64Only
    Build only the win-arm64 target.
.PARAMETER WinX64Only
    Build only the win-x64 target.
.PARAMETER AndroidEmulatorOnly
    Build only the android emulator targets. Run tools/Install-AndroidBuildTools.ps1 first.
.PARAMETER AndroidDeviceOnly
    Build only the android device targets. Run tools/Install-AndroidBuildTools.ps1 first.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
Param(
    [switch]$Release,
    [switch]$Locked,
    [switch]$WinArm64Only,
    [switch]$WinX64Only,
    [switch]$AndroidEmulatorOnly,
    [switch]$AndroidDeviceOnly,
    [switch]$SkipCsBindings
)

$ErrorActionPreference = 'Stop'

$buildArgs = @()

if ($Locked) {
    $buildArgs += '--locked'
}

Push-Location $PSScriptRoot

if (-not $SkipCsBindings) {
    Write-Host "Generating C# bindings..."
    .\generate_cs_bindings.ps1
}

if ($AndroidEmulatorOnly -or $AndroidDeviceOnly) {
    $buildVerb = 'ndk'

    if (!$env:ANDROID_NDK_HOME) {
        throw "ANDROID_NDK_HOME environment variable is not set. Run tools/Install-AndroidBuildTools.ps1 first."
    }

    if ($AndroidEmulatorOnly) {
        $buildArgs += '--target=x86_64-linux-android'
    }

    if ($AndroidDeviceOnly) {
        $buildArgs += '--target=aarch64-linux-android'
    }

    $buildArgs += 'build'
}
else {
    $buildVerb = 'build'

    if ($WinArm64Only) {
        $rustTargets = , 'aarch64-pc-windows-msvc'
    }
    elseif ($WinX64Only) {
        $rustTargets = , 'x86_64-pc-windows-msvc'
    }
    else {
        $rustTargets = @(..\..\tools\Get-RustTargets.ps1)
    }

    $rustTargets | % { $buildArgs += "--target=$_" }
}

if ($Release) {
    $buildArgs += '-r'
}

if ($env:TF_BUILD) {
    Write-Host "##[command]cargo $buildVerb $buildArgs"
}
else {
    Write-Host "cargo $buildVerb $buildArgs"
}

cargo $buildVerb @buildArgs

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Pop-Location
