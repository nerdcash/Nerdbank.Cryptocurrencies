use std::{collections::HashMap, ops::Range};

use zcash_client_backend::{data_api::chain::BlockSource, proto::compact_formats::CompactBlock};
use zcash_primitives::consensus::BlockHeight;

type ChainError<WalletError, BlockSourceError> =
    zcash_client_backend::data_api::chain::error::Error<WalletError, BlockSourceError>;

pub(crate) struct BlockCache {
    blocks: HashMap<u32, CompactBlock>,
}

#[derive(Debug)]
pub enum BlockCacheError {
    BlockNotFound(u32),
}

impl std::fmt::Display for BlockCacheError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            BlockCacheError::BlockNotFound(height) => {
                write!(f, "Block not found in cache: {}", height)
            }
        }
    }
}

impl BlockCache {
    pub fn new() -> Self {
        Self {
            blocks: HashMap::new(),
        }
    }

    pub fn insert(&mut self, block: CompactBlock) {
        self.blocks.insert(block.height as u32, block);
    }

    pub fn insert_range(&mut self, blocks: Vec<CompactBlock>) {
        for block in blocks {
            self.insert(block);
        }
    }

    pub fn remove(&mut self, height: u32) -> Option<CompactBlock> {
        self.blocks.remove(&height)
    }

    pub fn remove_range(&mut self, range: &Range<BlockHeight>) {
        for height in u32::from(range.start)..range.end.into() {
            self.remove(height);
        }
    }

    pub fn truncate_to_height(&mut self, block_height: BlockHeight) {
        let limit = u32::from(block_height);
        self.blocks.retain(|k, _| k <= &limit);
    }
}

impl BlockSource for BlockCache {
    type Error = BlockCacheError;

    fn with_blocks<F, WalletErrT>(
        &self,
        from_height: Option<zcash_primitives::consensus::BlockHeight>,
        limit: Option<usize>,
        mut with_row: F,
    ) -> Result<(), ChainError<WalletErrT, Self::Error>>
    where
        F: FnMut(
            zcash_client_backend::proto::compact_formats::CompactBlock,
        ) -> Result<(), ChainError<WalletErrT, Self::Error>>,
    {
        let mut head = u32::from(from_height.unwrap_or(BlockHeight::from(0)));
        let max_exclusive = head.saturating_add(limit.unwrap_or(u32::MAX as usize) as u32);

        while head < max_exclusive {
            let block = match self.blocks.get(&head) {
                Some(b) => b,
                None => {
                    return Err(ChainError::BlockSource(BlockCacheError::BlockNotFound(
                        head,
                    )))
                }
            };

            with_row(block.to_owned())?;
            head += 1;
        }

        Ok(())
    }
}
