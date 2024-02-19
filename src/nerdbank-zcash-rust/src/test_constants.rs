use http::Uri;
use secrecy::{Secret, SecretVec};
use testdir::testdir;
use tonic::transport::Channel;
use zcash_client_backend::keys::UnifiedSpendingKey;
use zcash_client_backend::proto::service::{
    self, compact_tx_streamer_client::CompactTxStreamerClient, LightdInfo,
};
use zcash_primitives::{
    consensus::Network,
    zip32::AccountId,
    zip339::{Count, Mnemonic},
};

use crate::error::Error;
use crate::resilience::webrequest_with_retry;
use crate::{backing_store::Db, grpc::get_client, interop::DbInit, lightclient::parse_network};

lazy_static! {
    pub(crate) static ref TESTNET_LIGHTSERVER_ECC_URI: Uri =
        Uri::from_static("https://lightwalletd.testnet.electriccoin.co:9067/");
    pub(crate) static ref MAINNET_LIGHTSERVER_ECC_URI: Uri =
        Uri::from_static("https://mainnet.lightwalletd.com:9067/");
}

pub(crate) const MIN_CONFIRMATIONS: u32 = 3;
pub(crate) const VALID_SAPLING_TESTNET: &str =
    "ztestsapling15740genxvp99m3vut5q7dqm0da9l8nst2njae3kpu6e406peeypk0n78zue0hgxt5gmasaznnm0";

lazy_static! {
    pub(crate) static ref LIGHTSERVER_URI: Uri =
        crate::test_constants::TESTNET_LIGHTSERVER_ECC_URI.to_owned();
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
    let server_info = webrequest_with_retry(|| async {
        let mut client = get_client(LIGHTSERVER_URI.to_owned()).await.unwrap();
        Ok(client
            .get_lightd_info(service::Empty {})
            .await?
            .into_inner())
    })
    .await
    .unwrap();
    let client = get_client(LIGHTSERVER_URI.to_owned()).await.unwrap();
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

pub(crate) async fn create_account(
    setup: &mut TestSetup,
) -> Result<(Secret<Vec<u8>>, u64, AccountId, UnifiedSpendingKey), Error> {
    let seed: secrecy::Secret<Vec<u8>> =
        SecretVec::new(Mnemonic::generate(Count::Words24).to_seed("").to_vec());

    let birthday = setup.server_info.block_height.saturating_sub(100);
    let account = setup
        .db
        .add_account(&seed, birthday, &mut setup.client)
        .await?;
    Ok((seed, birthday, account.0, account.1))
}
