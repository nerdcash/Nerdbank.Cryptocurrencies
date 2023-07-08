use orchard::keys::{FullViewingKey, Scope, SpendingKey, DiversifierIndex};

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

fn get_raw_payment_address_from_fvk(fvk: &[u8; 96], diversifier_index: impl Into<DiversifierIndex>) -> Option<[u8; 43]> {
    let fvk = FullViewingKey::from_bytes(fvk);
    match fvk.is_some().into() {
        true => {
            let fvk = fvk.unwrap();
            Some(fvk.address_at(diversifier_index, Scope::External).to_raw_address_bytes())
        }
        false => None,
    }
}

#[no_mangle]
pub extern "C" fn get_orchard_raw_payment_address_from_fvk(
    fvk: *const [u8; 96],
    diversifier_index: *const [u8; 11],
    raw_payment_address: *mut [u8; 43],
) -> i32 {
    let fvk = unsafe { &*fvk };
    let diversifier_index = unsafe { &*diversifier_index };
    let raw_payment_address = unsafe { &mut *raw_payment_address };

    match get_raw_payment_address_from_fvk(fvk, diversifier_index.clone()) {
        Some(raw_payment_address_bytes) => {
            raw_payment_address.copy_from_slice(&raw_payment_address_bytes);
            0
        }
        None => -1,
    }
}
