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
.PARAMETER AndroidEmulatorOnly
    Build only the android emulator targets. Run tools/Install-AndroidBuildTools.ps1 first.
.PARAMETER AndroidDeviceOnly
    Build only the android device targets. Run tools/Install-AndroidBuildTools.ps1 first.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
Param(
    [switch]$Release,
    [switch]$WinArm64Only,
    [switch]$WinX64Only,
    [switch]$AndroidEmulatorOnly,
    [switch]$AndroidDeviceOnly,
    [switch]$SkipCsBindings
)

$buildArgs = @()

Push-Location $PSScriptRoot

if (-not $SkipCsBindings) {
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
        $rustTargets = @(..\..\azure-pipelines\Get-RustTargets.ps1)
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

if ($env:BUILD_BUILDID) {
    Write-Host "##[command]cargo build @buildArgs"
}
cargo build @buildArgs

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# Special handling for building the wasm32-unknown-unknown target as it requires the nightly build of rust.
if ($env:BUILD_BUILDID) {
    Write-Host "##[command]cargo +nightly build -Zbuild-std --target=wasm32-unknown-unknown"
}
cargo +nightly build -Zbuild-std --target=wasm32-unknown-unknown

$configPathSegment = 'debug'
if ($Release) { $configPathSegment = 'release' }
$rustTargets | % {
    New-Item -ItemType Directory -Path "..\..\obj\src\nerdbank-zcash-rust\$configPathSegment\$_" -Force | Out-Null
    Copy-Item "target/$_/$configPathSegment/*nerdbank_zcash_rust*" "..\..\obj\src\nerdbank-zcash-rust\$configPathSegment\$_"
}

Pop-Location
