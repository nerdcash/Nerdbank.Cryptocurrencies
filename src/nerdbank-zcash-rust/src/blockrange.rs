use std::{fmt, ops::Range};

use zcash_client_backend::proto::service::{self, BlockId};
use zcash_protocol::consensus::BlockHeight;

use crate::error::Error;

/// A (half-open) range bounded inclusively below and exclusively above (start..end).
/// The range start..end contains all values with start <= x < end. It is empty if start >= end.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BlockRange {
    block_range: Range<BlockHeight>,
}

impl fmt::Display for BlockRange {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}..{}", self.block_range.start, self.block_range.end,)
    }
}

impl std::convert::TryFrom<Range<BlockHeight>> for BlockRange {
    type Error = Error;

    fn try_from(block_range: Range<BlockHeight>) -> Result<Self, Self::Error> {
        if block_range.end < block_range.start {
            Err(Error::Internal(format!(
                "{:?} is invalid for BlockRange",
                block_range
            )))
        } else {
            Ok(Self { block_range })
        }
    }
}

impl From<BlockRange> for Range<BlockHeight> {
    fn from(val: BlockRange) -> Self {
        val.block_range
    }
}

impl From<&BlockRange> for service::BlockRange {
    fn from(val: &BlockRange) -> Self {
        service::BlockRange {
            start: Some(BlockId {
                height: val.block_range.start.into(),
                ..Default::default()
            }),
            end: Some(BlockId {
                height: u64::from(val.block_range.end) - 1, // end is exclusive for our BlockRange, inclusive for service::BlockRange
                ..Default::default()
            }),
        }
    }
}

impl BlockRange {
    /// Constructs a scan range from a Range.
    ///
    /// # Panics
    /// Panics if `block_range.end < block_range.start`.
    pub fn from_range(block_range: Range<BlockHeight>) -> Self {
        assert!(
            block_range.end >= block_range.start,
            "{:?} is invalid for BlockRange",
            block_range,
        );
        Self { block_range }
    }

    /// Constructs a scan range from its constituent parts.
    ///
    /// # Parameters
    /// - `start`: The starting block height of the range, inclusive.
    /// - `end`: The ending block height of the range, exclusive.
    ///
    /// # Panics
    /// Panics if `end < start`.
    pub fn from_parts(start: BlockHeight, end: BlockHeight) -> Self {
        Self::from_range(start..end)
    }

    pub fn start(&self) -> BlockHeight {
        self.block_range.start
    }

    pub fn end(&self) -> BlockHeight {
        self.block_range.end
    }

    /// Returns the range of block heights to be scanned.
    pub fn block_range(&self) -> &Range<BlockHeight> {
        &self.block_range
    }

    /// Returns `true` if the range contains no items.
    pub fn is_empty(&self) -> bool {
        self.block_range.is_empty()
    }
}
