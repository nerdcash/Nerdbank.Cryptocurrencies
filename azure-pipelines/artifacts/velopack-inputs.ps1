$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$ObjRoot = "$RepoRoot/obj/src/Nerdbank.Zcash.App/Nerdbank.Zcash.App.Desktop/velopack-data.json"
$BinRoot = "$RepoRoot/bin/publish"
if (!(Test-Path $BinRoot)) { return }

# Avoid publish the same artifact twice from two different agents.
if ($env:SYSTEM_PHASENAME -like '*_velopack') { return }

@{
    "$RepoRoot" = (Get-ChildItem -Recurse $ObjRoot,$BinRoot);
}
