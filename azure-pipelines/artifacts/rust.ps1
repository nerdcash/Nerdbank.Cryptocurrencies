if (!(Test-Path "$PSScriptRoot/../../obj/src/nerdbank-zcash-rust/")) { return }

$root = "$PSScriptRoot/../../obj/src/nerdbank-zcash-rust/"

@{
    $root = Get-ChildItem $root -Recurse
}
