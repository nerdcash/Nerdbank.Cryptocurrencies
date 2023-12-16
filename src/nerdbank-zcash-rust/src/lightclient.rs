use crate::{error::Error, grpc::get_client};
use http::Uri;
use zcash_client_backend::proto::service;

/// Gets the block height from the lightwalletd server.
/// This may not match the the latest block that has been sync'd to the wallet.
pub async fn get_block_height(uri: Uri) -> Result<u64, Error> {
    let mut client = get_client(uri).await?;
    let response = client
        .get_lightd_info(service::Empty {})
        .await?
        .into_inner();
    Ok(response.block_height)
}

#[cfg(test)]
mod tests {
    use crate::test_constants::TESTNET_LIGHTSERVER_URI;

    use super::*;

	#[tokio::test]
    async fn test_get_block_height() {
        let block_height = get_block_height(TESTNET_LIGHTSERVER_URI.to_owned()).await.unwrap();
        assert!(block_height > 100_000);
        println!("block_height: {}", block_height);
    }
}
