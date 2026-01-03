[CmdletBinding()]
param (
    [parameter()]
    [string]$Configuration = 'release'
)

$repoRoot = Resolve-Path "$PSScriptRoot/.."
$version = dotnet nbgv get-version -p $repoRoot/src/nerdbank-zcash-rust -v SimpleVersion
$plist = Get-Content $PSScriptRoot/Info.plist
$plist = $plist.Replace('$version$', $version)
$IntermediatePlistPath = "$repoRoot/obj/Info.plist"
Set-Content -Path $IntermediatePlistPath -Value $plist -Encoding utf8NoBOM
if ($IsMacOS) {
    plutil -convert binary1 $IntermediatePlistPath
    chmod +x $IntermediatePlistPath
}
else {
    Write-Warning "Skipped plutil invocation because this is not macOS."
}

# copy Info.plist and the binary into the appropriate .framework directory structure
# so that when NativeBindings.targets references it with ResolvedFileToPublish, it will be treated appropriately.
$RustTargetBaseDir = $null
if ($env:CARGO_TARGET_DIR) {
    # If set, this may be relative to the working directory of the process.
    $RustTargetBaseDir = Resolve-Path $env:CARGO_TARGET_DIR
}
else {
    # Prefer Cargo's authoritative view of the target directory (important when this repo is a workspace).
    $metadata = cargo metadata --format-version 1 --no-deps --manifest-path "$repoRoot/Cargo.toml" | ConvertFrom-Json
    $RustTargetBaseDir = $metadata.target_directory
}

if (-not $RustTargetBaseDir -or !(Test-Path $RustTargetBaseDir)) {
    throw "Unable to determine Cargo target directory. Computed: '$RustTargetBaseDir'."
}

$RustDylibFileName = "libnerdbank_zcash_rust.dylib"
$DeviceRustOutput = "$RustTargetBaseDir/aarch64-apple-ios/$Configuration/$RustDylibFileName"
$SimulatorX64RustOutput = "$RustTargetBaseDir/x86_64-apple-ios/$Configuration/$RustDylibFileName"
$SimulatorArm64RustOutput = "$RustTargetBaseDir/aarch64-apple-ios-sim/$Configuration/$RustDylibFileName"

$DeviceFrameworkDir = "$repoRoot/bin/$Configuration/device/nerdbank_zcash_rust.framework"
$SimulatorFrameworkDir = "$repoRoot/bin/$Configuration/simulator/nerdbank_zcash_rust.framework"
New-Item -Path $DeviceFrameworkDir, $SimulatorFrameworkDir -ItemType Directory -Force | Out-Null

Write-Host "Preparing Apple iOS and iOS-simulator frameworks"

Copy-Item $IntermediatePlistPath "$DeviceFrameworkDir/Info.plist"
Copy-Item $IntermediatePlistPath "$SimulatorFrameworkDir/Info.plist"
Write-Host "Created Info.plist with version $version"

if ($IsMacOS) {
    # Rename the binary that contains the arm64 architecture for device.
    lipo -create -output $DeviceFrameworkDir/nerdbank_zcash_rust $DeviceRustOutput
    install_name_tool -id "@rpath/nerdbank_zcash_rust.framework/nerdbank_zcash_rust" "$DeviceFrameworkDir/nerdbank_zcash_rust"
    chmod +x "$DeviceFrameworkDir/nerdbank_zcash_rust"

    # Create a universal binary that contains both arm64 and x64 architectures for simulator.
    lipo -create -output $SimulatorFrameworkDir/nerdbank_zcash_rust $SimulatorX64RustOutput $SimulatorArm64RustOutput
    install_name_tool -id "@rpath/nerdbank_zcash_rust.framework/nerdbank_zcash_rust" "$SimulatorFrameworkDir/nerdbank_zcash_rust"
    chmod +x "$SimulatorFrameworkDir/nerdbank_zcash_rust"
}
else {
    Copy-Item $SimulatorArm64RustOutput "$SimulatorArm64RustOutput/nerdbank_zcash_rust"
    Copy-Item $DeviceRustOutput "$DeviceFrameworkDir/nerdbank_zcash_rust"
    Write-Warning "Skipped critical steps because this is not macOS."
}
Write-Host "Copied nerdbank_zcash_rust to framework"

# Build the xcframework
$xcframeworkOutputDir = "$repoRoot/bin/$Configuration/nerdbank_zcash_rust.xcframework"
xcodebuild -create-xcframework -framework $SimulatorFrameworkDir -framework $DeviceFrameworkDir -output $xcframeworkOutputDir
Write-Host "Created nerdbank_zcash_rust.xcframework at $xcframeworkOutputDir"
