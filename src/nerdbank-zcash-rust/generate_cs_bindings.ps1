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
    cargo binstall -y uniffi-bindgen-cs --git https://github.com/nerdcash/uniffi-bindgen-cs --rev f1196841bd28467 # just beyond v0.8.2+v0.25.0
    if ($env:TF_BUILD) {
        Write-Host "##[endgroup]"
    }
}

$outDir = Resolve-Path "$PSScriptRoot\..\Nerdbank.Zcash\RustBindings"
$uniffiTomlPath = Resolve-Path "$PSScriptRoot\uniffi.toml"
$ffiUdlPath = Resolve-Path "$PSScriptRoot\src\ffi.udl"
uniffi-bindgen-cs `
    -c $uniffiTomlPath `
    -o $outDir `
    $ffiUdlPath
if ($LASTEXITCODE -ne 0) {
    throw "uniffi-bindgen-cs failed with exit code $LASTEXITCODE."
}

dotnet csharpier --include-generated $outDir
Copy-Item $outDir\LightWallet.cs $outDir\LightWallet.iOS.cs -Force

# Customize the lib name based on platform
Function Replace-LibName {
    param(
        [string]$FilePath,
        [string]$NewLibName
    )

    $content = Get-Content $FilePath
    $content = $content.Replace('LIBNAMEHERE', $NewLibName)
    Set-Content -Path $FilePath -Value $content -Encoding utf8NoBOM
}

Write-Host "Replacing lib name in LightWallet.cs"
Replace-LibName $outDir\LightWallet.cs nerdbank_zcash_rust
(Get-Content $outDir\LightWallet.cs | Select-String DllImport)[0]

Write-Host "Replacing lib name in LightWallet.iOS.cs"
Replace-LibName $outDir\LightWallet.iOS.cs '@rpath/nerdbank_zcash_rust.framework/nerdbank_zcash_rust'
(Get-Content $outDir\LightWallet.iOS.cs | Select-String DllImport)[0]

# If we're in CI and this changed the bindings, someone failed to commit the changes earlier.
# We should fail the build in that case.
if ($env:TF_BUILD) {
    git diff --exit-code $outDir
    if ($LASTEXITCODE -ne 0) {
        throw "Bindings changed. Please run this script locally and commit the changes before submitting."
    }
}
