use std::ffi::CString;

use orchard::{
    keys::{Diversifier, FullViewingKey, Scope, SpendingKey},
    zip32,
};
use zcash_client_backend::address::UnifiedAddress;
use zcash_primitives::consensus::MainNetwork;

const COIN_TYPE: u32 = 133;

fn get_fvk_from_spending_key(spending_key: &[u8; 32]) -> Option<[u8; 96]> {
    let sk = SpendingKey::from_bytes(*spending_key);
    match sk.is_some().into() {
        true => {
            let sk = sk.unwrap();
            Some(FullViewingKey::from(&sk).to_bytes())
        }
        false => None,
    }
}

#[no_mangle]
pub extern "C" fn get_orchard_fvk_bytes_from_sk_bytes(
    spending_key: *const [u8; 32],
    fvk: *mut [u8; 96],
) -> i32 {
    let spending_key = unsafe { &*spending_key };
    let fvk = unsafe { &mut *fvk };

    match get_fvk_from_spending_key(spending_key) {
        Some(fvk_bytes) => {
            fvk.copy_from_slice(&fvk_bytes);
            0
        }
        None => -1,
    }
}

fn get_raw_payment_address_from_fvk(fvk: &[u8; 96], d: Diversifier) -> Option<[u8; 43]> {
    let fvk = FullViewingKey::from_bytes(fvk);
    match fvk.is_some().into() {
        true => {
            let fvk = fvk.unwrap();
            Some(fvk.address(d, Scope::External).to_raw_address_bytes())
        }
        false => None,
    }
}

#[no_mangle]
pub extern "C" fn get_orchard_raw_payment_address_from_fvk(
    fvk: *const [u8; 96],
    d: *const [u8; 11],
    raw_payment_address: *mut [u8; 43],
) -> i32 {
    let fvk = unsafe { &*fvk };
    let d = unsafe { &*d };
    let raw_payment_address = unsafe { &mut *raw_payment_address };

    let d = Diversifier::from_bytes(*d);
    match get_raw_payment_address_from_fvk(fvk, d) {
        Some(raw_payment_address_bytes) => {
            raw_payment_address.copy_from_slice(&raw_payment_address_bytes);
            0
        }
        None => -1,
    }
}

fn get_orchard_address(seed: &[u8; 64], account: u32) -> Result<String, zip32::Error> {
    let orchard_sk = SpendingKey::from_zip32_seed(seed, COIN_TYPE, account)?;
    let fvk = FullViewingKey::from(&orchard_sk);
    let a = fvk.address_at(0u64, Scope::External);
    let orchard_address = UnifiedAddress::from_receivers(Some(a), None, None).unwrap();
    let orchard_address_str = orchard_address.encode(&MainNetwork);
    Ok(orchard_address_str)
}

#[no_mangle]
pub extern "C" fn get_orchard_address_from_seed(
    seed: *const [u8; 64],
    account: u32,
    orchard_address: *mut u8,
    orchard_address_length: u32,
) -> i32 {
    let seed = unsafe { &*seed };
    match get_orchard_address(seed, account) {
        Ok(address) => {
            let len = address.len();
            if len > orchard_address_length as usize {
                // Return the required length as a negative number.
                return -(len as i32);
            }

            // Copy address into the orchard_address out parameter.
            let c_str = CString::new(address).unwrap();
            let ptr = c_str.as_ptr();
            unsafe {
                std::ptr::copy_nonoverlapping(ptr, orchard_address as *mut i8, len as usize);
            }

            len as i32
        }
        Err(_) => -1000,
    }
}

#[cfg(test)]
mod tests {
    use zcash_primitives::zip339::Mnemonic;

    use super::*;

    #[test]
    fn get_known_orchard_address() {
        let seed = "badge bless baby bird anger wage memory extend word isolate equip faith";
        let mnemonic = Mnemonic::from_phrase(seed).expect("phrase is valid");
        let seed = mnemonic.to_seed("");

        let result = get_orchard_address(&seed, 0).expect("get_address failed");
        assert_eq!(result, "u1zpfqm4r0cc5ttvt4mft6nvyqe3uwsdcgx65s44sd3ar42rnkz7v9az0ez7dpyxvjcyj9x0sd89yy7635vn8fplwvg6vn4tr6wqpyxqaw");
    }
}
