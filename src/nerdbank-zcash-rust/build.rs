use std::env;
use std::ffi::OsStr;
use std::fs;
use std::path::PathBuf;
use std::process::Command;

/// The Clang version that the NDK version pinned in `gradle.properties` should be using.
const ANDROID_NDK_CLANG_VERSION: &str = "17";

fn main() -> Result<(), Box<dyn std::error::Error>> {
    uniffi::generate_scaffolding("src/ffi.udl").unwrap();
    setup_x86_64_android_workaround();
    setup_ios_workaround()?;
    Ok(())
}

fn setup_ios_workaround() -> Result<(), Box<dyn std::error::Error>> {
    let target_os = env::var("CARGO_CFG_TARGET_OS").expect("CARGO_CFG_TARGET_OS not set");
    if target_os != "ios" {
        return Ok(());
    }

    let target = env::var("TARGET").unwrap_or_default();
    let target_arch = env::var("CARGO_CFG_TARGET_ARCH").unwrap_or_default();
    let clang_arch = match target_arch.as_str() {
        "aarch64" => "arm64",
        "x86_64" => "x86_64",
        other => other,
    };
    let is_simulator = target.contains("apple-ios-sim")
        || target.starts_with("x86_64-apple-ios")
        || target.starts_with("i386-apple-ios");
    let platform_suffix = if is_simulator { "iossim" } else { "ios" };
    let sdk = if is_simulator {
        "iphonesimulator"
    } else {
        "iphoneos"
    };

    let clang = Command::new("xcrun")
        .args(["--sdk", sdk, "--find", "clang"])
        .output()?
        .stdout;
    let clang = String::from_utf8(clang)?.trim().to_string();
    if clang.is_empty() {
        return Err(format!("Unable to locate clang via xcrun for sdk {sdk}.").into());
    }

    let resource_dir = Command::new(&clang)
        .arg("--print-resource-dir")
        .output()?
        .stdout;
    let resource_dir = String::from_utf8(resource_dir)?.trim().to_string();
    if resource_dir.is_empty() {
        return Err(format!("Unable to query clang resource dir using {clang}.").into());
    }

    let link_path = PathBuf::from(resource_dir).join("lib").join("darwin");
    if !link_path.exists() {
        return Err(format!(
            "Expected clang runtime dir does not exist: {}",
            link_path.display()
        )
        .into());
    }

    let candidates = [
        // Prefer arch-specific libraries (common in Xcode toolchains).
        format!("clang_rt.builtins_{clang_arch}_{platform_suffix}"),
        // Fallback to non-arch-specific names.
        format!("clang_rt.builtins_{platform_suffix}"),
        // Some toolchains expose a "dynamic" archive as well.
        format!("clang_rt.builtins_{platform_suffix}_dynamic"),
    ];

    let mut selected: Option<String> = None;

    for candidate in candidates {
        if link_path.join(format!("lib{candidate}.a")).is_file() {
            selected = Some(candidate);
            break;
        }
    }

    if selected.is_none() {
        // Last resort: scan for any builtins archive matching the platform suffix.
        let entries = fs::read_dir(&link_path).map_err(|e| {
            format!(
                "Unable to enumerate clang runtime directory {}: {e}",
                link_path.display()
            )
        })?;

        let mut builtins = entries
            .filter_map(|e| e.ok())
            .filter_map(|e| e.file_name().into_string().ok())
            .filter(|name| {
                name.ends_with(".a") && name.contains("builtins") && name.contains(platform_suffix)
            })
            .collect::<Vec<_>>();
        builtins.sort();

        if let Some(first) = builtins
            .iter()
            .find(|n| n.contains(clang_arch))
            .or_else(|| builtins.first())
        {
            selected = Some(
                first
                    .trim_start_matches("lib")
                    .trim_end_matches(".a")
                    .to_string(),
            );
        } else {
            return Err(format!(
                "Could not find a clang builtins library for target {target} (arch={clang_arch}, sdk={sdk}). Looked in {}",
                link_path.display()
            )
            .into());
        }
    }

    let selected = selected.expect("selected must be set");

    println!("cargo:rustc-link-search={}", link_path.display());
    println!("cargo:rustc-link-lib=static={selected}");
    Ok(())
}

/// Adds a temporary workaround for [an issue] with the Rust compiler and Android when
/// compiling for x86_64 devices.
///
/// The Android NDK used to include `libgcc` for unwind support (which is required by Rust
/// among others). From NDK r23, `libgcc` is removed, replaced by LLVM's `libunwind`.
/// However, `libgcc` was ambiently providing other compiler builtins, one of which we
/// require: `__extenddftf2` for software floating-point emulation. This is used by SQLite
/// (via the `rusqlite` crate), which defines a `LONGDOUBLE_TYPE` type as `long double`.
///
/// Rust uses a `compiler-builtins` crate that does not provide `__extenddftf2` because
/// [it involves floating-point types that are not supported by Rust][unsupported]. For
/// some reason, they _do_ export this symbol for `aarch64-linux-android`, but they do not
/// for `x86_64-linux-android`. Thus we run into a problem when trying to compile and run
/// the SDK on an x86_64 emulator.
///
/// The workaround comes from [this Mozilla PR]: we tell Cargo to statically link the
/// builtins from the Clang runtime provided inside the NDK, to provide this symbol.
///
/// [an issue]: https://github.com/rust-lang/rust/issues/109717
/// [this Mozilla PR]:https://github.com/mozilla/application-services/pull/5442
/// [unsupported]: https://github.com/rust-lang/compiler-builtins#unimplemented-functions
fn setup_x86_64_android_workaround() {
    let target_os = env::var("CARGO_CFG_TARGET_OS").expect("CARGO_CFG_TARGET_OS not set");
    let target_arch = env::var("CARGO_CFG_TARGET_ARCH").expect("CARGO_CFG_TARGET_ARCH not set");

    if target_arch == "x86_64" && target_os == "android" {
        let cc = if let Some(cc) = env::var_os("RUST_ANDROID_GRADLE_CC") {
            // We are building in the context of the `org.mozilla.rust-android-gradle`
            // plugin, which knows where the NDK is and provides the Clang path.
            PathBuf::from(cc)
        } else {
            // We are probably building directly on the CLI. Construct a path to Clang.
            let android_ndk_home =
                env::var_os("ANDROID_NDK_HOME").expect("ANDROID_NDK_HOME not set");
            let build_os = match env::consts::OS {
                "linux" => "linux",
                "macos" => "darwin",
                "windows" => "windows",
                _ => panic!(
                    "Unsupported OS. You must use either Linux, MacOS or Windows to build the crate."
                ),
            };

            let mut cc = PathBuf::from(android_ndk_home);
            cc.push("toolchains");
            cc.push("llvm");
            cc.push("prebuilt");
            cc.push(format!("{build_os}-x86_64"));
            cc.push("bin");
            cc.push("clang");
            cc.set_extension(env::consts::EXE_EXTENSION);
            cc
        };

        let mut link_path = cc
            .ancestors()
            .nth(2)
            .expect("path format is known")
            .join("lib");
        link_path.push("clang");
        link_path.push(get_clang_version(&cc));
        link_path.push("lib");
        link_path.push("linux");

        if link_path.exists() {
            println!("cargo:rustc-link-search={}", link_path.display());
            println!("cargo:rustc-link-lib=static=clang_rt.builtins-x86_64-android");
        } else {
            panic!("Path {} does not exist", link_path.display());
        }
    }
}

fn get_clang_version(cc: impl AsRef<OsStr>) -> String {
    let clang_version_output = Command::new(cc)
        .arg("--version")
        .output()
        .ok()
        .and_then(|o| String::from_utf8(o.stdout).ok());
    clang_version_output
        .as_deref()
        .and_then(|s| s.split_once("clang version "))
        .and_then(|(_, s)| s.split_once('.'))
        .map(|(major_version, _)| major_version)
        // If we couldn't run Clang for some reason, default to the Clang version that the
        // NDK version pinned in `gradle.properties` should be using.
        .unwrap_or(ANDROID_NDK_CLANG_VERSION)
        .into()
}
