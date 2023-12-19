use http::Uri;

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
