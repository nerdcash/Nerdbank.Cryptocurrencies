if ($IsLinux) {
    ,'x86_64-unknown-linux-gnu'
}
elseif ($IsMacOS) {
    ,'aarch64-apple-darwin'
    ,'x86_64-apple-darwin'
    ,'aarch64-apple-ios'     # device
    ,'x86_64-apple-ios'      # simulator
    ,'aarch64-apple-ios-sim' # simulator
}
else { # Windows
    ,'aarch64-pc-windows-msvc'
    ,'x86_64-pc-windows-msvc'
}
