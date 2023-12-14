use zcash_client_backend::{data_api::chain::error::Error as backend_error, scanning::ScanError};

#[derive(Debug)]
pub enum Error<WalletError, BlockSourceError> {
    /// An error occurred over a transport.
    Transport(tonic::transport::Error),

    /// An error that was produced by wallet operations in the course of scanning the chain.
    Wallet(WalletError),

    /// An error that was produced by the underlying block data store in the process of validation
    /// or scanning.
    BlockSource(BlockSourceError),

    /// A block that was received violated rules related to chain continuity or contained note
    /// commitments that could not be reconciled with the note commitment tree(s) maintained by the
    /// wallet.
    Scan(ScanError),

    TonicStatus(tonic::Status),
}

impl<WalletError, BlockSourceError> From<tonic::transport::Error>
    for Error<WalletError, BlockSourceError>
{
    fn from(e: tonic::transport::Error) -> Self {
        Error::Transport(e)
    }
}

impl<WalletError, BlockSourceError> From<backend_error<WalletError, BlockSourceError>>
    for Error<WalletError, BlockSourceError>
{
    fn from(e: backend_error<WalletError, BlockSourceError>) -> Self {
        match e {
            backend_error::Wallet(e) => Error::Wallet(e),
            backend_error::BlockSource(e) => Error::BlockSource(e),
            backend_error::Scan(e) => Error::Scan(e),
        }
    }
}

impl<WalletError, BlockSourceError> From<tonic::Status> for Error<WalletError, BlockSourceError> {
    fn from(e: tonic::Status) -> Self {
        Error::TonicStatus(e)
    }
}
