$RepoRoot = Resolve-Path $PSScriptRoot\..\..
$ObjRoot = "$RepoRoot/obj/src/Nerdbank.Zcash.App/Nerdbank.Zcash.App.Desktop/velopack-data.json"
$BinRoot = "$RepoRoot/bin/publish"
if (!(Test-Path $BinRoot)) { return }

# Avoid publish the same artifact twice from two different agents.
if ($env:SYSTEM_PHASENAME -like '*_velopack') { return }

$Include = @(Get-ChildItem -Recurse $ObjRoot,$BinRoot)

if ($IsMacOS) {
    "arm64","x64" |% {
        $Path = "$RepoRoot/bin/Nerdbank.Zcash.App.Desktop/Release/net8.0-macos/osx-$_/Nerdbank.Zcash.App.Desktop.app"
        if (Test-Path $Path) {
            $Include += Get-ChildItem -Recurse $Path
        }
    }
}

@{
    "$RepoRoot" = $Include;
}
