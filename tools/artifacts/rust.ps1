$repoRoot = Resolve-Path "$PSScriptRoot/../.."

$root = $null
if ($env:CARGO_TARGET_DIR) {
    $root = Resolve-Path $env:CARGO_TARGET_DIR
}
else {
    $metadata = cargo metadata --format-version 1 --no-deps --manifest-path "$repoRoot/Cargo.toml" | ConvertFrom-Json
    $root = $metadata.target_directory
}

if (!(Test-Path $root)) { return }

$files = @()
Get-ChildItem $root\*-*-* -Directory |% {
    $files += Get-ChildItem "$($_.FullName)\*\*nerdbank_zcash_rust*"
}

@{
    $root = $files
}
