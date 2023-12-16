use http::Uri;
use std::{cell::RefCell, collections::HashMap, sync::Mutex};
use tonic::transport::{Channel, ClientTlsConfig};

// We'll use a MUTEX to store the shareable gRPC channels, indexed by server URI.
// gRPC channels are expensive to create, cannot be used concurrently, but cheap to clone for each user.
lazy_static! {
    static ref CHANNELS: Mutex<HashMap<Uri, RefCell<Channel>>> = Mutex::new(HashMap::new());
}

/// This function will return a gRPC channel for the given URI, creating one if necessary.
pub(crate) async fn get_grpc_channel(uri: Uri) -> Result<Channel, tonic::transport::Error> {
    let mut clients = CHANNELS.lock().unwrap();
    if let Some(client) = clients.get(&uri) {
        let channel = &*client.borrow();
        return Ok(channel.clone());
    }

    let tls = ClientTlsConfig::new().domain_name(uri.host().unwrap());
    let channel = Channel::builder(uri.clone())
        .tls_config(tls)?
        .connect()
        .await?;
    clients.insert(uri, RefCell::new(channel.clone()));
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
