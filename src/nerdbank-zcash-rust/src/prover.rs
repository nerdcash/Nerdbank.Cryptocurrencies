use zcash_proofs::{download_sapling_parameters, prover::LocalTxProver};

use crate::error::Error;

pub(crate) fn get_prover() -> Result<LocalTxProver, Error> {
    let parameter_paths = download_sapling_parameters(None)?;
    Ok(LocalTxProver::new(
        &parameter_paths.spend,
        &parameter_paths.output,
    ))
}
