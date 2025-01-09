#!/usr/bin/env pwsh

# NDK version
$version = '26c'

if ($IsLinux) {
    $os = 'linux'
}
elseif ($IsMacOS) {
    $os = 'darwin'
}
else {
    $os = 'windows'
}

Function Get-FileFromWeb([Uri]$Uri, $OutFile) {
    if (!(Test-Path $OutFile)) {
        Write-Verbose "Downloading $Uri..."
        $OutDir = Split-Path $OutFile
        if (!(Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }
        try {
            (New-Object System.Net.WebClient).DownloadFile($Uri, $OutFile)
        } finally {
            # This try/finally causes the script to abort
        }
    }
}

$installRoot = "$PSScriptRoot/../obj/tools"
if (!(Test-Path $installRoot)) { New-Item -ItemType Directory -Path $installRoot -WhatIf:$false | Out-Null }
$installRoot = Resolve-Path $installRoot

### Install NDK

if ($env:TF_BUILD) {
    Write-Host "##[group]Android NDK acquisition"
} else {
    Write-Host "Acquiring Android NDK" -ForegroundColor Cyan
}

$ndkHome = Join-Path $installRoot "android-ndk-r$version"

if (!(Test-Path $ndkHome)) {
    $NdkZipPath = "$installRoot\android-ndk.zip"
    if (!(Test-Path $NdkZipPath)) {
        Write-Host "Downloading NDK r$version for $os"
        Get-FileFromWeb -Uri "https://dl.google.com/android/repository/android-ndk-r$version-$os.zip" -OutFile $NdkZipPath
    }

    Write-Host "Extracting NDK to $ndkHome"
    unzip -q $NdkZipPath -d $installRoot
}
else {
    Write-Host "NDK already installed at $ndkHome"
}

& "$PSScriptRoot\Set-EnvVars.ps1" @{
    ANDROID_NDK_HOME = $ndkHome
    ANDROID_NDK_ROOT = $ndkHome
    ANDROID_NDK = $ndkHome
}

if ($env:TF_BUILD) {
    Write-Host "##[endgroup]"
}

### Install cargo-ndk

if ($env:TF_BUILD) {
    Write-Host "##[group]cargo-ndk acquisition"
    Write-Host "##[command]cargo install --version ^3 cargo-ndk"
} else {
    Write-Host "Acquiring cargo ndk" -ForegroundColor Cyan
}

cargo binstall -y --version ^3 cargo-ndk

if ($env:TF_BUILD) {
    Write-Host "##[endgroup]"
}

### Install rustup targets

if ($env:TF_BUILD) {
    Write-Host "##[group]rustup"
    Write-Host "##[command]rustup target add aarch64-linux-android x86_64-linux-android"
} else {
    Write-Host "Adding rustup targets" -ForegroundColor Cyan
}

rustup target add aarch64-linux-android x86_64-linux-android

if ($env:TF_BUILD) {
    Write-Host "##[endgroup]"
}
