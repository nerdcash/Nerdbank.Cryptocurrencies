use std::ffi::CString;

use zcash_client_backend::encoding::encode_payment_address;
use zcash_primitives::{
    consensus::{MainNetwork, Parameters},
    sapling::keys::{ExpandedSpendingKey, FullViewingKey},
    zip32::{ChildIndex, DiversifiableFullViewingKey, DiversifierIndex, ExtendedSpendingKey},
};

const PURPOSE: u32 = 32;
const COIN_TYPE: u32 = 133;

#[no_mangle]
pub extern "C" fn derive_sapling_child(
    ext_sk: *const [u8; 169],
    child_index: u32,
    child: *mut [u8; 169],
) -> i32 {
    let ext_sk = unsafe { &*ext_sk };
    let child_index = ChildIndex::from_index(child_index);
    let child_bytes = unsafe { &mut *child };

    // Do the same thing as derive_child, but write the result to the child variable and return an error code when it fails.
    match ExtendedSpendingKey::from_bytes(ext_sk) {
        Ok(sk) => {
            let derived_child = sk.derive_child(child_index);
            child_bytes.copy_from_slice(&derived_child.to_bytes());
            0
        }
        Err(_) => -1,
    }
}

#[no_mangle]
pub extern "C" fn get_sapling_expanded_sk(sk: *const [u8; 32], expsk: *mut [u8; 96]) {
    let sk = unsafe { &*sk };
    let expsk_bytes = unsafe { &mut *expsk };

    let expsk = ExpandedSpendingKey::from_spending_key(sk);
    expsk_bytes.copy_from_slice(&expsk.to_bytes());
}

#[no_mangle]
pub extern "C" fn get_sapling_fvk_from_expanded_sk(
    expsk: *const [u8; 96],
    fvk: *mut [u8; 96],
) -> i32 {
    let expsk = unsafe { &*expsk };
    let fvk_bytes = unsafe { &mut *fvk };

    match ExpandedSpendingKey::from_bytes(expsk) {
        Ok(expsk) => {
            let fvk = FullViewingKey::from_expanded_spending_key(&expsk);
            match fvk.write(&mut fvk_bytes[..]) {
                Ok(_) => 0,
                _ => -2,
            }
        }
        _ => -1,
    }
}

fn get_sapling_address(seed: &[u8; 64], account: u32) -> String {
    let spending_key = ExtendedSpendingKey::master(seed);
    let (_, payment_address) = spending_key
        .derive_child(ChildIndex::Hardened(PURPOSE))
        .derive_child(ChildIndex::Hardened(COIN_TYPE))
        .derive_child(ChildIndex::Hardened(account))
        .default_address();
    encode_payment_address(MainNetwork.hrp_sapling_payment_address(), &payment_address)
}

#[no_mangle]
pub extern "C" fn get_sapling_receiver(
    fvk: *const [u8; 96],
    dk: *const [u8; 32],
    diversifier_index: *mut [u8; 11],
    receiver: *mut [u8; 43],
) -> i32 {
    let fvk = unsafe { &*fvk };
    let dk = unsafe { &*dk };
    let diversifier_index = unsafe { &mut *diversifier_index };
    let j = DiversifierIndex {
        0: diversifier_index.clone(),
    };
    let receiver = unsafe { &mut *receiver };

    let mut fvk_dk = [0u8; 128];
    fvk_dk[..96].copy_from_slice(fvk);
    fvk_dk[96..].copy_from_slice(dk);
    if let Some(dfvk) = DiversifiableFullViewingKey::from_bytes(&fvk_dk) {
        if let Some((index, recv)) = dfvk.find_address(j) {
            receiver.copy_from_slice(&recv.to_bytes());
            diversifier_index.copy_from_slice(&index.0);
            return 0;
        }

        return -2;
    }

    -1
}

#[no_mangle]
pub extern "C" fn get_sapling_address_from_seed(
    seed: *const [u8; 64],
    account: u32,
    orchard_address: *mut u8,
    orchard_address_length: u32,
) -> i32 {
    let seed = unsafe { &*seed };
    let address = get_sapling_address(seed, account);
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

#[cfg(test)]
mod tests {
    use zcash_primitives::zip339::Mnemonic;

    use super::*;

    #[test]
    fn get_known_sapling_address() {
        let seed = "badge bless baby bird anger wage memory extend word isolate equip faith";
        let mnemonic = Mnemonic::from_phrase(seed).expect("phrase is valid");
        let seed = mnemonic.to_seed("");

        let result = get_sapling_address(&seed, 0);
        assert_eq!(
            result,
            "zs1duqpcc2ql7zfjttdm2gpawe8t5ecek5k834u9vdg4mqhw7j8j39sgjy8xguvk2semyd4ujeyj28"
        );
    }
}
