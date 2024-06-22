use http::Uri;
use tonic::transport::{Channel, ClientTlsConfig};
use zcash_client_backend::proto::service::compact_tx_streamer_client::CompactTxStreamerClient;

// This was meant to share and reuse gRPC channels, but it was buggy and caused test instability.
// Check https://users.rust-lang.org/t/is-my-concurrent-use-of-grpc-safe/113328 in case a fix is found.

/// Return a gRPC channel for the given URI, creating one if necessary.
pub(crate) async fn get_grpc_channel(uri: Uri) -> Result<Channel, tonic::transport::Error> {
    let tls = ClientTlsConfig::new().domain_name(uri.host().unwrap());
    let channel = Channel::builder(uri.clone())
        .tls_config(tls)?
        .connect()
        .await?;

    Ok(channel)
}

/// This function will release a gRPC channel for the given URI from the shared map, if it exists.
/// Concurrent users will still be able to use the channel, but it will be destroyed when the last
/// user disconnects.
///
/// The function returns true if the channel was found and removed, false otherwise.
pub(crate) fn destroy_channel(_uri: Uri) -> bool {
    false
}

/// Gets the CompactTxStreamerClient for the given URI for use with communicating with the lightwalletd server.
pub async fn get_client(
    uri: Uri,
) -> Result<CompactTxStreamerClient<Channel>, tonic::transport::Error> {
    let channel = get_grpc_channel(uri).await?;
    Ok(CompactTxStreamerClient::new(channel))
}

#[cfg(test)]
mod tests {
    use crate::test_constants::LIGHTSERVER_URI;

    use super::*;

    #[tokio::test]
    async fn get_client_twice_then_destroy() {
        get_client(LIGHTSERVER_URI.to_owned()).await.unwrap();
        get_client(LIGHTSERVER_URI.to_owned()).await.unwrap();
        assert!(!destroy_channel(LIGHTSERVER_URI.to_owned()));
    }
}
