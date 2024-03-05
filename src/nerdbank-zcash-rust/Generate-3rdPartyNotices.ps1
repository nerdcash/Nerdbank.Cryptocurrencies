Push-Location $PSScriptRoot

cargo install cargo-bundle-licenses

New-Item -Type Directory -Path $PSScriptRoot/../../obj/src/nerdbank-zcash-rust | Out-Null
cargo tree -f "{p} {l}" -e normal | Out-File -Encoding UTF8 -FilePath "$PSScriptRoot/../../obj/src/nerdbank-zcash-rust/THIRD_PARTY_DEPENDENCIES.txt"
cargo bundle-licenses --format yaml --output "$PSScriptRoot/../../obj/src/nerdbank-zcash-rust/THIRD_PARTY_LICENSES.yml"

Pop-Location
