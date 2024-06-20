use bip0039::{Count, English, Mnemonic};
use http::Uri;
use secrecy::{Secret, SecretVec};
use testdir::testdir;
use tokio_util::sync::CancellationToken;
use tonic::transport::Channel;
use zcash_client_backend::data_api::Account;
use zcash_client_backend::keys::UnifiedSpendingKey;
use zcash_client_backend::proto::service::{
    self, compact_tx_streamer_client::CompactTxStreamerClient, LightdInfo,
};
use zcash_client_sqlite::AccountId;
use zcash_primitives::consensus::Network;

use crate::error::Error;
use crate::interop::SyncUpdateData;
use crate::resilience::webrequest_with_retry;
use crate::sync::sync;
use crate::{backing_store::Db, grpc::get_client, interop::DbInit, lightclient::parse_network};

lazy_static! {
    pub(crate) static ref TESTNET_LIGHTSERVER_URI: Uri =
        Uri::from_static("https://zcash.mysideoftheweb.com:19067/");
    pub(crate) static ref MAINNET_LIGHTSERVER_URI: Uri =
        Uri::from_static("https://zcash.mysideoftheweb.com:9067/");
}

pub(crate) const MIN_CONFIRMATIONS: u32 = 3;
pub(crate) const VALID_SAPLING_TESTNET: &str =
    "ztestsapling15740genxvp99m3vut5q7dqm0da9l8nst2njae3kpu6e406peeypk0n78zue0hgxt5gmasaznnm0";

lazy_static! {
    pub(crate) static ref LIGHTSERVER_URI: Uri =
        crate::test_constants::TESTNET_LIGHTSERVER_URI.to_owned();
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
    let server_info = webrequest_with_retry(
        || async {
            let mut client = get_client(LIGHTSERVER_URI.to_owned()).await.unwrap();
            Ok(client
                .get_lightd_info(service::Empty {})
                .await?
                .into_inner())
        },
        CancellationToken::new(),
    )
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
            min_confirmations: 3,
        },
        db,
        server_info,
        server_uri: LIGHTSERVER_URI.to_owned(),
    }
}

impl TestSetup {
    pub async fn create_account(
        &mut self,
    ) -> Result<(Secret<Vec<u8>>, u64, AccountId, UnifiedSpendingKey), Error> {
        let seed: secrecy::Secret<Vec<u8>> = SecretVec::new(
            Mnemonic::<English>::generate(Count::Words24)
                .to_seed("")
                .to_vec(),
        );

        let birthday = self.server_info.block_height.saturating_sub(100);
        let account = self
            .db
            .add_account(&seed, zip32::AccountId::ZERO, birthday, &mut self.client)
            .await?;
        Ok((seed, birthday, account.0.id(), account.1))
    }

    pub async fn sync(&mut self) -> SyncUpdateData {
        sync(
            self.server_uri.clone(),
            &self.data_file,
            None,
            self.db_init.min_confirmations,
            false,
            CancellationToken::new(),
        )
        .await
        .unwrap()
    }
}
