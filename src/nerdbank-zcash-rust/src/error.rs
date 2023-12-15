use schemer::MigratorError;
use uniffi::deps::anyhow;
use zcash_client_backend::{data_api::chain::error::Error as ChainError, scanning::ScanError};
use zcash_client_sqlite::{
    error::SqliteClientError, wallet::init::WalletMigrationError, FsBlockDbError,
};

#[derive(Debug)]
pub enum Error {
    /// An error occurred over a transport.
    Transport(tonic::transport::Error),

    /// An error that was produced by wallet operations in the course of scanning the chain.
    Wallet(SqliteClientError),

    /// An error that was produced by the underlying block data store in the process of validation
    /// or scanning.
    BlockSource(FsBlockDbError),

    /// A block that was received violated rules related to chain continuity or contained note
    /// commitments that could not be reconciled with the note commitment tree(s) maintained by the
    /// wallet.
    Scan(ScanError),

    TonicStatus(tonic::Status),

    FsBlockDbError(FsBlockDbError),

    IoError(std::io::Error),

    InternalError(String),

    Sqlite(rusqlite::Error),

    SqliteClient(SqliteClientError),

    SqliteMigratorError(MigratorError<rusqlite::Error>),

    WalletMigratorError(MigratorError<WalletMigrationError>),

    Anyhow(anyhow::Error),
}

impl From<tonic::transport::Error>
    for Error
{
    fn from(e: tonic::transport::Error) -> Self {
        Error::Transport(e)
    }
}

impl From<ChainError<SqliteClientError, FsBlockDbError>>
    for Error
{
    fn from(e: ChainError<SqliteClientError, FsBlockDbError>) -> Self {
        match e {
            ChainError::Wallet(e) => Error::Wallet(e),
            ChainError::BlockSource(e) => Error::BlockSource(e),
            ChainError::Scan(e) => Error::Scan(e),
        }
    }
}

impl From<tonic::Status> for Error {
    fn from(e: tonic::Status) -> Self {
        Error::TonicStatus(e)
    }
}

impl From<FsBlockDbError> for Error {
    fn from(e: FsBlockDbError) -> Self {
        Error::FsBlockDbError(e)
    }
}

impl From<rusqlite::Error> for Error {
    fn from(e: rusqlite::Error) -> Self {
        Error::Sqlite(e)
    }
}

impl From<MigratorError<rusqlite::Error>>
    for Error
{
    fn from(e: MigratorError<rusqlite::Error>) -> Self {
        Error::SqliteMigratorError(e)
    }
}

impl From<MigratorError<WalletMigrationError>>
    for Error
{
    fn from(e: MigratorError<WalletMigrationError>) -> Self {
        Error::WalletMigratorError(e)
    }
}

impl From<std::io::Error> for Error {
    fn from(e: std::io::Error) -> Self {
        Error::IoError(e)
    }
}

impl From<SqliteClientError>
    for Error
{
    fn from(e: SqliteClientError) -> Self {
        Error::SqliteClient(e)
    }
}

impl From<anyhow::Error> for Error {
    fn from(e: anyhow::Error) -> Self {
        Error::Anyhow(e)
    }
}
