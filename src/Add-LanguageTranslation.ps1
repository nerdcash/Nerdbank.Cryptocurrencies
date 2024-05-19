<#
.SYNOPSIS
    Copies culture-neutral .resx files to language-specific .resx files.
.DESCRIPTION
    After copying the files, each file must be translated manually or using AI, which this script does *not* automate.
.PARAMETER Language
    The language code to use for the language-specific .resx files.
    Use the .NET culture code (e.g. `es`, `pt`, `zh-Hans`).
#>

[CmdletBinding(SupportsShouldProcess = $true)]
Param (
    [Parameter(Mandatory=$true)]
    [string]$Language
)

gci $PSScriptRoot\*.resx -Exclude *.*.resx -Recurse |% {
    $TargetPath = Join-Path ($_.Directory) ($_.BaseName + "." + $Language + ".resx")
    if (!(Test-Path $TargetPath)) {
        Copy-Item $_ $TargetPath
    }
}
