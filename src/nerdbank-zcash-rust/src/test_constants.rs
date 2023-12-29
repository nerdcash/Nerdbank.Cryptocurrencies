use http::Uri;
use testdir::testdir;
use tonic::transport::Channel;
use zcash_client_backend::proto::service::{
    self, compact_tx_streamer_client::CompactTxStreamerClient, LightdInfo,
};
use zcash_primitives::consensus::Network;

use crate::{backing_store::Db, grpc::get_client, interop::DbInit, lightclient::parse_network};

lazy_static! {
    pub(crate) static ref TESTNET_LIGHTSERVER_URI: Uri =
        Uri::from_static("https://zcash.mysideoftheweb.com:19067");
    pub(crate) static ref MAINNET_LIGHTSERVER_URI: Uri =
        Uri::from_static("https://zcash.mysideoftheweb.com:9067");
    pub(crate) static ref TESTNET_LIGHTSERVER_ECC_URI: Uri =
        Uri::from_static("https://lightwalletd.testnet.electriccoin.co:9067/");
    pub(crate) static ref MAINNET_LIGHTSERVER_ECC_URI: Uri =
        Uri::from_static("https://mainnet.lightwalletd.com:9067/");
}

pub(crate) const MIN_CONFIRMATIONS: u32 = 3;

lazy_static! {
    static ref LIGHTSERVER_URI: Uri = crate::test_constants::TESTNET_LIGHTSERVER_ECC_URI.to_owned();
}

pub(crate) struct TestSetup {
    pub(crate) client: CompactTxStreamerClient<Channel>,
    pub(crate) network: Network,
    pub(crate) data_file: std::path::PathBuf,
    pub(crate) db_init: DbInit,
    pub(crate) db: Db,
    pub(crate) server_info: LightdInfo,
    pub(crate) server_uri: Uri,
}

pub(crate) async fn setup_test() -> TestSetup {
    let wallet_dir = testdir!();
    let mut client = get_client(LIGHTSERVER_URI.to_owned()).await.unwrap();
    let server_info = client
        .get_lightd_info(service::Empty {})
        .await
        .unwrap()
        .into_inner();
    let network = parse_network(&server_info).unwrap();
    let data_file = wallet_dir.join("wallet.sqlite");
    let db = Db::init(&data_file, network).unwrap();
    TestSetup {
        client,
        network,
        data_file: data_file.clone(),
        db_init: DbInit {
            data_file: data_file.into_os_string().into_string().unwrap(),
            network: network.into(),
        },
        db,
        server_info,
        server_uri: LIGHTSERVER_URI.to_owned(),
    }
}
