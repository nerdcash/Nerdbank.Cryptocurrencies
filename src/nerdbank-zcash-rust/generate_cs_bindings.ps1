#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Generate C# bindings for the exported rust functions. If uniffi-bindgen-cs is not installed, it will be installed automatically.
.PARAMETER InstallPrerequisites
    Install uniffi-bindgen-cs. Default is true if it is not already installed.
#>

[CmdletBinding()]
Param (
    [switch]$InstallPrerequisites = $null -eq (Get-Command uniffi-bindgen-cs -ErrorAction SilentlyContinue)
)

if ($InstallPrerequisites) {
    if ($env:TF_BUILD) {
        Write-Host "##[group]Install uniffi-bindgen-cs"
    }
    cargo install uniffi-bindgen-cs --git https://github.com/nerdcash/uniffi-bindgen-cs --rev 17de10a1f121b12b7534eafefd4696319e9d2418 # --tag v0.8.0+v0.25.0
    if ($env:TF_BUILD) {
        Write-Host "##[endgroup]"
    }
}

$outDir = "$PSScriptRoot\..\Nerdbank.Zcash\RustBindings"
uniffi-bindgen-cs `
    -c $PSScriptRoot\uniffi.toml `
    -o $outDir `
    $PSScriptRoot\src\ffi.udl
dotnet csharpier --include-generated (Resolve-Path $outDir)
