use std::ffi::CString;

use zcash_client_backend::encoding::encode_payment_address;
use zcash_primitives::{zip32::{ExtendedSpendingKey, ChildIndex}, consensus::{MainNetwork, Parameters}};

const PURPOSE: u32 = 32;
const COIN_TYPE: u32 = 133;

fn get_sapling_address(seed: &[u8; 64], account: u32) -> String {
    let spending_key = ExtendedSpendingKey::master(seed);
    let (_ , payment_address) = spending_key
        .derive_child(ChildIndex::Hardened(PURPOSE))
        .derive_child(ChildIndex::Hardened(COIN_TYPE))
        .derive_child(ChildIndex::Hardened(account))
        .default_address();
    encode_payment_address(MainNetwork.hrp_sapling_payment_address(), &payment_address)
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
        assert_eq!(result, "zs1duqpcc2ql7zfjttdm2gpawe8t5ecek5k834u9vdg4mqhw7j8j39sgjy8xguvk2semyd4ujeyj28");
    }
}
