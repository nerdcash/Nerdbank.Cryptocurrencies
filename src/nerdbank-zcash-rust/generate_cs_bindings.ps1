#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Generate C# bindings for the exported rust functions.
.PARAMETER InstallPrerequisites
    Installs uniffi-bindgen-cs.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
Param(
    [switch]$InstallPrerequisites
)

if ($InstallPrerequisites) {
    cargo install uniffi-bindgen-cs --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.7.0+v0.25.0
}

uniffi-bindgen-cs `
    -c $PSScriptRoot\uniffi.toml `
    -o $PSScriptRoot\..\Nerdbank.Zcash\RustBindings `
    $PSScriptRoot\src\ffi.udl
