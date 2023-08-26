use group::GroupEncoding;
use zcash_primitives::{
    sapling::{
        keys::{ExpandedSpendingKey, FullViewingKey, ViewingKey},
        NullifierDerivingKey, PaymentAddress, SaplingIvk,
    },
    zip32::{
        sapling::DiversifierKey, ChildIndex, DiversifiableFullViewingKey, DiversifierIndex,
        ExtendedFullViewingKey, ExtendedSpendingKey, Scope,
    },
};

#[no_mangle]
pub extern "C" fn derive_sapling_ivk_from_fvk(
    ak: *const [u8; 32],
    nk: *const [u8; 32],
    ivk: *mut [u8; 32],
) -> i32 {
    let ak = unsafe { &*ak };
    let nk = unsafe { &*nk };
    let ivk = unsafe { &mut *ivk };

    let ak = jubjub::SubgroupPoint::from_bytes(ak);
    if ak.is_none().into() {
        return -1;
    }

    let ak = ak.unwrap();
    let nk = jubjub::SubgroupPoint::from_bytes(nk);
    if nk.is_none().into() {
        return -2;
    }

    let nk = NullifierDerivingKey(nk.unwrap());

    ivk.copy_from_slice(&ViewingKey { ak, nk }.ivk().to_repr());

    0
}

#[no_mangle]
pub extern "C" fn decrypt_sapling_diversifier(
    fvk: *const [u8; 96],
    dk: *const [u8; 32],
    receiver: *const [u8; 43],
    diversifier_index: *mut [u8; 11],
    scope: *mut u8,
) -> i32 {
    let fvk = unsafe { &*fvk };
    let dk = unsafe { &*dk };
    let receiver = unsafe { &*receiver };
    let diversifier_index = unsafe { &mut *diversifier_index };
    let scope = unsafe { &mut *scope };

    let mut fvk_dk = [0u8; 128];
    fvk_dk[..96].copy_from_slice(fvk);
    fvk_dk[96..].copy_from_slice(dk);

    if let Some(dfvk) = DiversifiableFullViewingKey::from_bytes(&fvk_dk) {
        if let Some(receiver) = PaymentAddress::from_bytes(receiver) {
            if let Some((idx, s)) = dfvk.decrypt_diversifier(&receiver) {
                diversifier_index.copy_from_slice(&idx.0);
                *scope = match s {
                    Scope::External => 0,
                    Scope::Internal => 1,
                };
                0
            } else {
                // Everything was valid. But the receiver did *not* come from this key.
                1
            }
        } else {
            -2
        }
    } else {
        -1
    }
}

#[no_mangle]
pub extern "C" fn decrypt_sapling_diversifier_with_ivk(
    ivk: *const [u8; 32],
    dk: *const [u8; 32],
    receiver: *const [u8; 43],
    diversifier_index: *mut [u8; 11],
) -> i32 {
    let ivk = unsafe { &*ivk };
    let dk = unsafe { &*dk };
    let receiver = unsafe { &*receiver };
    let diversifier_index = unsafe { &mut *diversifier_index };

    let fr = jubjub::Fr::from_bytes(ivk);
    if fr.is_none().into() {
        return -1;
    }
    let ivk = SaplingIvk(fr.unwrap());

    let dk = DiversifierKey::from_bytes(*dk);

    let address = PaymentAddress::from_bytes(receiver);
    if address.is_none().into() {
        return -2;
    }
    let address = address.unwrap();

    let j = dk.diversifier_index(address.diversifier());

    // Now use the ivk to generate a receiver with this diversifier index.
    // If the receiver matches the one that came in, then this ivk produced the given receiver.
    // If it doesn't match, then this ivk did not produce the given receiver so no index is correct.
    let regenerated_diversifier = dk.diversifier(j);
    if regenerated_diversifier.is_none() {
        return -3;
    }
    let regenerated_address = ivk.to_payment_address(regenerated_diversifier.unwrap());
    if regenerated_address == Some(address) {
        diversifier_index.copy_from_slice(&j.0);
        0
    } else {
        // Everything was valid. But the receiver did *not* come from this key.
        1
    }
}

#[no_mangle]
pub extern "C" fn derive_sapling_child_fvk(
    ext_fvk: *const [u8; 169],
    child_index: u32,
    child: *mut [u8; 169],
) -> i32 {
    let ext_fvk = unsafe { &*ext_fvk };
    let child_index = ChildIndex::from_index(child_index);
    let child_bytes = unsafe { &mut *child };

    // Do the same thing as derive_child, but write the result to the child variable and return an error code when it fails.
    if let Ok(fvk) = ExtendedFullViewingKey::read(&ext_fvk[..]) {
        if let Ok(derived_child) = fvk.derive_child(child_index) {
            if derived_child.write(&mut child_bytes[..]).is_ok() {
                return 0;
            }
        }
    }

    return -1;
}

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

#[no_mangle]
pub extern "C" fn get_sapling_receiver(
    ivk: *const [u8; 32],
    dk: *const [u8; 32],
    diversifier_index: *mut [u8; 11],
    receiver: *mut [u8; 43],
) -> i32 {
    let ivk = unsafe { &*ivk };
    let dk = unsafe { &*dk };
    let diversifier_index = unsafe { &mut *diversifier_index };
    let receiver = unsafe { &mut *receiver };
    let dk = DiversifierKey::from_bytes(*dk);

    let j = DiversifierIndex {
        0: diversifier_index.clone(),
    };

    let fr = jubjub::Fr::from_bytes(ivk);
    if fr.is_some().into() {
        let ivk = SaplingIvk(fr.unwrap());
        if let Some((index, d)) = dk.find_diversifier(j) {
            diversifier_index.copy_from_slice(&index.0);
            if let Some(addr) = ivk.to_payment_address(d) {
                receiver.copy_from_slice(&addr.to_bytes());
                return 0;
            } else {
                -3
            }
        } else {
            -2
        }
    } else {
        -1
    }
}
