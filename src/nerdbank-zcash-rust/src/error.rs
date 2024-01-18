use schemer::MigratorError;
use uniffi::deps::anyhow;
use zcash_client_backend::{
    data_api::{chain::error::Error as ChainError, BirthdayError},
    scanning::ScanError,
    zip321::Zip321Error,
};
use zcash_client_sqlite::{error::SqliteClientError, wallet::init::WalletMigrationError};
use zcash_primitives::{memo, transaction::components::amount::NonNegativeAmount};

use crate::block_source::BlockCacheError;

type BackendError<DataSourceError, CommitmentTreeError, SelectionError, FeeError> =
    zcash_client_backend::data_api::error::Error<
        DataSourceError,
        CommitmentTreeError,
        SelectionError,
        FeeError,
    >;

#[derive(Debug)]
pub enum Error {
    /// An error occurred over a transport.
    Transport(tonic::transport::Error),

    /// An error that was produced by wallet operations in the course of scanning the chain.
    Wallet(SqliteClientError),

    /// An error that was produced by the underlying block data store in the process of validation
    /// or scanning.
    BlockSource(BlockCacheError),

    /// A block that was received violated rules related to chain continuity or contained note
    /// commitments that could not be reconciled with the note commitment tree(s) maintained by the
    /// wallet.
    Scan(ScanError),

    TonicStatus(tonic::Status),

    IoError(std::io::Error),

    InternalError(String),

    Sqlite(rusqlite::Error),

    SqliteClient(SqliteClientError),

    SqliteMigratorError(MigratorError<rusqlite::Error>),

    WalletMigratorError(MigratorError<WalletMigrationError>),

    InvalidHeight,

    InvalidAmount,

    InsufficientFunds {
        required: NonNegativeAmount,
        available: NonNegativeAmount,
    },

    InvalidAddress,

    InvalidMemo(memo::Error),

    Zip321Error(Zip321Error),

    /// The wallet has not been synced to the chain yet, and thus has no data with which to formulate a response.
    SyncFirst,

    InvalidArgument(String),

    Anyhow(anyhow::Error),

    SendFailed {
        code: i32,
        reason: String,
    },
}

impl std::fmt::Display for Error {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Error::Transport(e) => e.fmt(f),
            Error::Wallet(e) => e.fmt(f),
            Error::BlockSource(e) => e.fmt(f),
            Error::Scan(e) => e.fmt(f),
            Error::TonicStatus(e) => e.fmt(f),
            Error::IoError(e) => e.fmt(f),
            Error::InternalError(e) => e.fmt(f),
            Error::Sqlite(e) => e.fmt(f),
            Error::SqliteClient(e) => e.fmt(f),
            Error::SqliteMigratorError(e) => e.fmt(f),
            Error::WalletMigratorError(e) => e.fmt(f),
            Error::InvalidHeight => f.write_str("Invalid height"),
            Error::InvalidAmount => f.write_str("Invalid amount"),
            Error::InsufficientFunds {
                required,
                available,
            } => write!(
                f,
                "Insufficient funds: required {} ZATs, available {} ZATs",
                u64::from(*required),
                u64::from(*available)
            ),
            Error::InvalidAddress => f.write_str("Invalid address"),
            Error::InvalidMemo(e) => e.fmt(f),
            Error::Zip321Error(e) => e.fmt(f),
            Error::SyncFirst => f.write_str("Sync before performing this operation."),
            Error::InvalidArgument(e) => e.fmt(f),
            Error::Anyhow(e) => e.fmt(f),
            Error::SendFailed { code, reason } => write!(f, "Send failed: {}: {}", code, reason),
        }
    }
}

impl From<tonic::transport::Error> for Error {
    fn from(e: tonic::transport::Error) -> Self {
        Error::Transport(e)
    }
}

impl From<ChainError<SqliteClientError, BlockCacheError>> for Error {
    fn from(e: ChainError<SqliteClientError, BlockCacheError>) -> Self {
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

impl From<BirthdayError> for Error {
    fn from(e: BirthdayError) -> Self {
        match e {
            BirthdayError::Decode(e) => Error::IoError(e),
            BirthdayError::HeightInvalid(_) => Error::InvalidHeight,
        }
    }
}

impl From<BlockCacheError> for Error {
    fn from(e: BlockCacheError) -> Self {
        Error::BlockSource(e)
    }
}

impl From<rusqlite::Error> for Error {
    fn from(e: rusqlite::Error) -> Self {
        Error::Sqlite(e)
    }
}

impl From<MigratorError<rusqlite::Error>> for Error {
    fn from(e: MigratorError<rusqlite::Error>) -> Self {
        Error::SqliteMigratorError(e)
    }
}

impl From<MigratorError<WalletMigrationError>> for Error {
    fn from(e: MigratorError<WalletMigrationError>) -> Self {
        Error::WalletMigratorError(e)
    }
}

impl From<std::io::Error> for Error {
    fn from(e: std::io::Error) -> Self {
        Error::IoError(e)
    }
}

impl From<SqliteClientError> for Error {
    fn from(e: SqliteClientError) -> Self {
        Error::SqliteClient(e)
    }
}

impl From<anyhow::Error> for Error {
    fn from(e: anyhow::Error) -> Self {
        Error::Anyhow(e)
    }
}

impl From<memo::Error> for Error {
    fn from(e: memo::Error) -> Self {
        Error::InvalidMemo(e)
    }
}

impl From<Zip321Error> for Error {
    fn from(e: Zip321Error) -> Self {
        Error::Zip321Error(e)
    }
}

impl<DataSourceError, CommitmentTreeError, SelectionError, FeeError>
    From<BackendError<DataSourceError, CommitmentTreeError, SelectionError, FeeError>> for Error
where
    DataSourceError: std::fmt::Display + std::fmt::Debug,
    CommitmentTreeError: std::fmt::Display + std::fmt::Debug,
    SelectionError: std::fmt::Display + std::fmt::Debug,
    FeeError: std::fmt::Display + std::fmt::Debug,
{
    fn from(
        value: BackendError<DataSourceError, CommitmentTreeError, SelectionError, FeeError>,
    ) -> Self {
        match value {
            BackendError::DataSource(inner) => {
                Error::InternalError(format!("DataSource: {}", inner))
            }
            BackendError::CommitmentTree(inner) => {
                Error::InternalError(format!("CommitmentTree: {}", inner))
            }
            BackendError::NoteSelection(inner) => {
                Error::InternalError(format!("NoteSelection: {}", inner))
            }
            BackendError::KeyNotRecognized => Error::InternalError("KeyNotRecognized".to_string()),
            BackendError::AccountNotFound(id) => {
                Error::InternalError(format!("AccountNotFound: {}", u32::from(id)))
            }
            BackendError::BalanceError(inner) => {
                Error::InternalError(format!("BalanceError: {}", inner))
            }
            BackendError::InsufficientFunds {
                available,
                required,
            } => Error::InsufficientFunds {
                required,
                available,
            },
            BackendError::ScanRequired => Error::SyncFirst,
            BackendError::Builder(inner) => Error::InternalError(format!("Builder: {}", inner)),
            BackendError::MemoForbidden => Error::InternalError("MemoForbidden".to_string()),
            BackendError::NoteMismatch(_) => Error::InternalError("NoteMismatch".to_string()),
            BackendError::AddressNotRecognized(_) => Error::InvalidAddress,
            BackendError::ChildIndexOutOfRange(_) => {
                Error::InternalError("ChildIndexOutOfRange".to_string())
            }
            BackendError::UnsupportedPoolType(e) => {
                Error::InternalError(format!("UnsupportedPoolType: {}", e))
            }
        }
    }
}
