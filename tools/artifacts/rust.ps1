$repoRoot = Resolve-Path "$PSScriptRoot/../.."

function Add-IfPresent {
    param(
        [System.Collections.Generic.HashSet[string]]$Set,
        [string]$Path
    )

    if (-not $Path) {
        return
    }

    if (Test-Path $Path) {
        $resolved = (Resolve-Path $Path).Path
        [void]$Set.Add($resolved)
    }
}

function Get-CargoTargetDirectory {
    param(
        [string]$RepoRoot
    )

    try {
        $metadata = cargo metadata --format-version 1 --no-deps --manifest-path "$RepoRoot/Cargo.toml" | ConvertFrom-Json
        return $metadata.target_directory
    }
    catch {
        return $null
    }
}

function Get-RustFilesUnderTargetRoot {
    param(
        [string]$TargetRoot
    )

    $files = @()
    if (!(Test-Path $TargetRoot)) {
        return $files
    }

    Get-ChildItem (Join-Path $TargetRoot '*-*-*') -Directory -ErrorAction SilentlyContinue | % {
        # Expect outputs under: <target>/<triple>/<profile>/... and also deps.
        $files += Get-ChildItem (Join-Path $_.FullName '*') -Recurse -Filter '*nerdbank_zcash_rust*' -ErrorAction SilentlyContinue
    }

    $files
}

$roots = New-Object 'System.Collections.Generic.HashSet[string]'

# Preferred: Cargo's workspace-aware target directory.
Add-IfPresent -Set $roots -Path (Get-CargoTargetDirectory -RepoRoot $repoRoot)

# Include commonly used conventional locations (older scripts used these).
Add-IfPresent -Set $roots -Path (Join-Path $repoRoot 'target')
Add-IfPresent -Set $roots -Path (Join-Path $repoRoot 'target-host')
Add-IfPresent -Set $roots -Path (Join-Path $repoRoot 'src/nerdbank-zcash-rust/target')
Add-IfPresent -Set $roots -Path (Join-Path $repoRoot 'src/nerdbank-zcash-rust/target-host')

# If CI sets CARGO_TARGET_DIR, include plausible resolutions for both repo-root and crate-root invocations.
if ($env:CARGO_TARGET_DIR) {
    $cargoTargetDir = $env:CARGO_TARGET_DIR
    if ([System.IO.Path]::IsPathRooted($cargoTargetDir)) {
        Add-IfPresent -Set $roots -Path $cargoTargetDir
    }
    else {
        Add-IfPresent -Set $roots -Path (Join-Path (Get-Location) $cargoTargetDir)
        Add-IfPresent -Set $roots -Path (Join-Path $repoRoot $cargoTargetDir)
        Add-IfPresent -Set $roots -Path (Join-Path $repoRoot (Join-Path 'src/nerdbank-zcash-rust' $cargoTargetDir))
    }
}

if ($roots.Count -eq 0) {
    return
}

$result = @{}
foreach ($root in $roots) {
    $files = Get-RustFilesUnderTargetRoot -TargetRoot $root
    if ($files -and $files.Count -gt 0) {
        $result[$root] = $files
    }
}

if ($result.Count -eq 0) {
    return
}

$result
