use crate::{error::Error, grpc::get_client, resilience::webrequest_with_retry};
use http::Uri;
use tokio_util::sync::CancellationToken;
use zcash_client_backend::proto::service::{self, LightdInfo};
use zcash_primitives::consensus::Network;

/// Gets the block height from the lightwalletd server.
/// This may not match the the latest block that has been sync'd to the wallet.
pub async fn get_block_height(
    uri: Uri,
    cancellation_token: CancellationToken,
) -> Result<u32, Error> {
    let client = get_client(uri).await?;
    let response = webrequest_with_retry(
        || async {
            Ok(client
                .clone()
                .get_lightd_info(service::Empty {})
                .await?
                .into_inner())
        },
        cancellation_token,
    )
    .await?;
    Ok(response.block_height as u32)
}

pub(crate) fn parse_network(info: &LightdInfo) -> Result<Network, Error> {
    match info.chain_name.as_str() {
        "main" => Ok(Network::MainNetwork),
        "test" => Ok(Network::TestNetwork),
        _ => Err(Error::Internal(format!(
            "Unknown network: {}",
            info.chain_name
        ))),
    }
}

#[cfg(test)]
mod tests {
    use crate::test_constants::LIGHTSERVER_URI;

    use super::*;

    #[tokio_shared_rt::test]
    async fn test_get_block_height() {
        let block_height = get_block_height(LIGHTSERVER_URI.to_owned(), CancellationToken::new())
            .await
            .unwrap();
        assert!(block_height > 100_000);
        println!("block_height: {}", block_height);
    }
}
