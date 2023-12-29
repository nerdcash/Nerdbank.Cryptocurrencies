if ($IsLinux) {
#    ,'aarch64-unknown-linux-gnu'
    ,'x86_64-unknown-linux-gnu'
}
elseif ($IsMacOS) {
    ,'aarch64-apple-darwin'
    ,'x86_64-apple-darwin'
}
else { # Windows
#    ,'aarch64-pc-windows-msvc'
    ,'x86_64-pc-windows-msvc'
}
