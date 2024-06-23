use http::Uri;
use std::{collections::HashMap, sync::Mutex};
use tonic::transport::{Channel, ClientTlsConfig};
use zcash_client_backend::proto::service::compact_tx_streamer_client::CompactTxStreamerClient;

// We'll use a MUTEX to store the shareable gRPC channels, indexed by server URI.
// gRPC channels are expensive to create, cannot be used concurrently, but cheap to clone for each user.
lazy_static! {
    static ref CHANNELS: Mutex<HashMap<Uri, Channel>> = Mutex::new(HashMap::new());
}

/// Return a gRPC channel for the given URI, creating one if necessary.
pub(crate) async fn get_grpc_channel(uri: Uri) -> Result<Channel, tonic::transport::Error> {
    {
        let clients = CHANNELS.lock().unwrap();
        if let Some(channel) = clients.get(&uri) {
            return Ok(channel.clone());
        }
    }

    let tls = ClientTlsConfig::new().domain_name(uri.host().unwrap());
    let channel = Channel::builder(uri.clone())
        .tls_config(tls)?
        .connect()
        .await?;

    let mut clients = CHANNELS.lock().unwrap();
    clients.insert(uri, channel.clone());
    Ok(channel)
}

/// This function will release a gRPC channel for the given URI from the shared map, if it exists.
/// Concurrent users will still be able to use the channel, but it will be destroyed when the last
/// user disconnects.
///
/// The function returns true if the channel was found and removed, false otherwise.
pub(crate) fn destroy_channel(uri: Uri) -> bool {
    let mut clients = CHANNELS.lock().unwrap();
    clients.remove(&uri).is_some()
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

    #[tokio_shared_rt::test]
    async fn get_client_twice_then_destroy() {
        get_client(LIGHTSERVER_URI.to_owned()).await.unwrap();
        get_client(LIGHTSERVER_URI.to_owned()).await.unwrap();
        assert!(destroy_channel(LIGHTSERVER_URI.to_owned()));
    }
}
