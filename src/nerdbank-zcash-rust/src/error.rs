use schemer::MigratorError;
use tokio::task::JoinError;
use uniffi::deps::anyhow;
use zcash_client_backend::{
    data_api::{chain::error::Error as ChainError, BirthdayError},
    scanning::ScanError,
    zip321::Zip321Error,
};
use zcash_client_sqlite::{error::SqliteClientError, wallet::init::WalletMigrationError};
use zcash_primitives::{
    memo,
    transaction::components::amount::{BalanceError, NonNegativeAmount},
};

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

    HDWallet(hdwallet::error::Error),

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

    Balance(BalanceError),

    Io(std::io::Error),

    Minreq(minreq::Error),

    Internal(String),

    Sqlite(rusqlite::Error),

    SqliteClient(SqliteClientError),

    SqliteMigrator(MigratorError<rusqlite::Error>),

    WalletMigrator(MigratorError<WalletMigrationError>),

    InvalidHeight,

    InvalidAmount,

    InsufficientFunds {
        required: NonNegativeAmount,
        available: NonNegativeAmount,
    },

    InvalidAddress,

    InvalidMemo(memo::Error),

    Zip321(Zip321Error),

    /// The operation requires an outpoint whose value is not known.
    OutPointMissing,

    /// The wallet has not been synced to the chain yet, and thus has no data with which to formulate a response.
    SyncFirst,

    InvalidArgument(String),

    Anyhow(anyhow::Error),

    SendFailed {
        code: i32,
        reason: String,
    },

    ProposalNotSupported,

    NoSpendingKey(String),

    KeyNotRecognized,

    Join(JoinError),

    Canceled,
}

impl std::fmt::Display for Error {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Error::Transport(e) => e.fmt(f),
            Error::HDWallet(e) => e.fmt(f),
            Error::Wallet(e) => e.fmt(f),
            Error::BlockSource(e) => e.fmt(f),
            Error::Scan(e) => e.fmt(f),
            Error::TonicStatus(e) => e.fmt(f),
            Error::Io(e) => e.fmt(f),
            Error::Internal(e) => e.fmt(f),
            Error::Sqlite(e) => e.fmt(f),
            Error::SqliteClient(e) => e.fmt(f),
            Error::SqliteMigrator(e) => e.fmt(f),
            Error::WalletMigrator(e) => e.fmt(f),
            Error::InvalidHeight => f.write_str("Invalid height"),
            Error::Balance(e) => e.fmt(f),
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
            Error::Zip321(e) => e.fmt(f),
            Error::SyncFirst => f.write_str("Sync before performing this operation."),
            Error::InvalidArgument(e) => e.fmt(f),
            Error::Anyhow(e) => e.fmt(f),
            Error::SendFailed { code, reason } => write!(f, "Send failed: {}: {}", code, reason),
            Error::Minreq(e) => e.fmt(f),
            Error::OutPointMissing => f.write_str("OutPoint missing"),
            Error::ProposalNotSupported => f.write_str("Proposal not supported"),
            Error::NoSpendingKey(e) => write!(f, "No spending key: {}", e),
            Error::KeyNotRecognized => f.write_str("No account found with the given key."),
            Error::Join(e) => e.fmt(f),
            Error::Canceled => f.write_str("Canceled"),
        }
    }
}

impl From<BalanceError> for Error {
    fn from(e: BalanceError) -> Self {
        Error::Balance(e)
    }
}

impl From<JoinError> for Error {
    fn from(e: JoinError) -> Self {
        Error::Join(e)
    }
}

impl From<tonic::transport::Error> for Error {
    fn from(e: tonic::transport::Error) -> Self {
        Error::Transport(e)
    }
}

impl From<hdwallet::error::Error> for Error {
    fn from(e: hdwallet::error::Error) -> Self {
        Error::HDWallet(e)
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
            BirthdayError::Decode(e) => Error::Io(e),
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
        Error::SqliteMigrator(e)
    }
}

impl From<MigratorError<WalletMigrationError>> for Error {
    fn from(e: MigratorError<WalletMigrationError>) -> Self {
        Error::WalletMigrator(e)
    }
}

impl From<std::io::Error> for Error {
    fn from(e: std::io::Error) -> Self {
        Error::Io(e)
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
        Error::Zip321(e)
    }
}

impl From<minreq::Error> for Error {
    fn from(value: minreq::Error) -> Self {
        Error::Minreq(value)
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
            BackendError::DataSource(inner) => Error::Internal(format!("DataSource: {}", inner)),
            BackendError::CommitmentTree(inner) => {
                Error::Internal(format!("CommitmentTree: {}", inner))
            }
            BackendError::NoteSelection(inner) => {
                Error::Internal(format!("NoteSelection: {}", inner))
            }
            BackendError::KeyNotRecognized => Error::KeyNotRecognized,
            BackendError::BalanceError(inner) => {
                Error::Internal(format!("BalanceError: {}", inner))
            }
            BackendError::InsufficientFunds {
                available,
                required,
            } => Error::InsufficientFunds {
                required,
                available,
            },
            BackendError::ScanRequired => Error::SyncFirst,
            BackendError::Builder(inner) => Error::Internal(format!("Builder: {}", inner)),
            BackendError::MemoForbidden => Error::Internal("MemoForbidden".to_string()),
            BackendError::NoteMismatch(_) => Error::Internal("NoteMismatch".to_string()),
            BackendError::AddressNotRecognized(_) => Error::InvalidAddress,
            BackendError::ProposalNotSupported => Error::ProposalNotSupported,
            BackendError::NoSpendingKey(msg) => Error::NoSpendingKey(msg),
            BackendError::UnsupportedChangeType(pool_type) => {
                Error::Internal(format!("UnsupportedChangeType: {}", pool_type))
            }
            BackendError::Proposal(e) => Error::Internal(format!("Proposal: {}", e)),
            BackendError::NoSupportedReceivers(_) => {
                Error::Internal("No supported receivers".to_string())
            }
        }
    }
}
