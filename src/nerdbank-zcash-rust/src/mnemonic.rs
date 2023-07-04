use std::ffi::CStr;

use zcash_primitives::zip339::Mnemonic;

#[no_mangle]
pub extern "C" fn get_seed_from_mnemonic_phrase(
    phrase: *const u8,
    passphrase: *const u8,
    seed: *mut [u8; 64],
) -> i32 {
    // Convert the phrase and passphrase to Rust slices.
    // Return a negative number if the phrase or passphrase is not valid UTF-8.
    let phrase = match unsafe { CStr::from_ptr(phrase as *const i8).to_str() } {
        Ok(phrase) => phrase,
        Err(_) => return -1,
    };
    let passphrase = match unsafe { CStr::from_ptr(passphrase as *const i8).to_str() } {
        Ok(passphrase) => passphrase,
        Err(_) => return -2,
    };

    let mnemonic = match Mnemonic::from_phrase(phrase) {
        Ok(mnemonic) => mnemonic,
        Err(_) => return -3,
    };
    let seed = unsafe { &mut *seed };
    let seed_bytes = mnemonic.to_seed(passphrase);
    seed.copy_from_slice(&seed_bytes);
    0
}
