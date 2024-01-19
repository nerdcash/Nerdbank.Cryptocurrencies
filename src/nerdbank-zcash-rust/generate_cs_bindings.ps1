#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Generate C# bindings for the exported rust functions. If uniffi-bindgen-cs is not installed, it will be installed automatically.
#>

# Check if uniiffi-bindgen-cs is installed
[bool]$InstallPrerequisites = $null -eq (Get-Command uniffi-bindgen-cs -ErrorAction SilentlyContinue)

if ($InstallPrerequisites) {
    cargo install uniffi-bindgen-cs --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.7.0+v0.25.0
}

uniffi-bindgen-cs `
    -c $PSScriptRoot\uniffi.toml `
    -o $PSScriptRoot\..\Nerdbank.Zcash\RustBindings `
    $PSScriptRoot\src\ffi.udl
