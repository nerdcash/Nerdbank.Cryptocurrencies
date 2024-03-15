#!/usr/bin/env pwsh

# NDK version
$version = '26c'

if ($IsLinux) {
    $os = 'linux'
}
elseif ($IsMacOS) {
    $os = 'darwin'
}
elseif ($IsWindows) {
    $os = 'windows'
}
else {
    throw "Unsupported OS"
}

Function SetEnv($name, $value) {
    Write-Host "Setting env var $name=$value"
    if ($env:TF_BUILD) {
        Write-Host "##vso[task.setvariable variable=$name;]$value"
    }
    Set-Item -Path "env:$name" -Value $value
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
        curl -L "https://dl.google.com/android/repository/android-ndk-r$version-$os.zip" -o $NdkZipPath
    }

    unzip -q $NdkZipPath -d $installRoot
}
else {
    Write-Host "NDK already installed at $ndkHome"
}

SetEnv 'ANDROID_NDK_HOME' $ndkHome
SetEnv 'ANDROID_NDK_ROOT' $ndkHome
SetEnv 'ANDROID_NDK' $ndkHome

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

cargo install --version ^3 cargo-ndk

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
