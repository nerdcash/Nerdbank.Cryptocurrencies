$root = "$PSScriptRoot\..\..\src\nerdbank-zcash-rust\target"
if (!(Test-Path $root)) { return }

$files = @()
Get-ChildItem $root\*-*-* -Directory |% {
    $files += Get-ChildItem "$($_.FullName)\*\*nerdbank_zcash_rust*"
}

@{
    $root = $files
}
