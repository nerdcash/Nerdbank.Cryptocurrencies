# Features


See this library [running live via Blazor in your web browser](https://zcash.nerdbank.net/).

Please be advised of the following:

- 🚫🕵🏻 This code currently is not audited by an external security auditor, use it at your own risk.
- 🚫🕵🏻 The code has not been subjected to thorough review by engineers at the Electric Coin Company or anywhere else.
- 🚫 Sending and receiving funds in the Orchard pool does not yet work. See [librustzcash#404](https://github.com/zcash/librustzcash/issues/404) and [librustzcash#403](https://github.com/zcash/librustzcash/issues/403).
- ⚠️ Transparent funds must be shielded before they can be spent.
- 🚧 We are actively adding features and fixing bugs.

## Nerdbank.Cryptocurrencies

[![NuGet package](https://img.shields.io/nuget/v/Nerdbank.Cryptocurrencies.svg)](https://nuget.org/packages/Nerdbank.Cryptocurrencies)

* Cryptography functions that are common to many cryptocurrencies, such as:
  * Base58Check
  * Bech32 and Bech32m
  * Blake2B
  * Compact Size

BIPs, and more...

[Learn more about this package.](https://github.com/nerdcash/Nerdbank.Cryptocurrencies/blob/main/src/Nerdbank.Cryptocurrencies/README.md)

## Nerdbank.Zcash

[![NuGet package](https://img.shields.io/nuget/v/Nerdbank.Zcash.svg)](https://nuget.org/packages/Nerdbank.Zcash)

* Parse and construct Zcash addresses, including transparent, shielded and unified addresses.
* Lightclient functionality.

[Learn more about this package.](https://github.com/nerdcash/Nerdbank.Cryptocurrencies/blob/main/src/Nerdbank.Zcash/README.md)

This project [sponsored in part](https://zfnd.org/wp-content/uploads/2023/04/Unified_Address_library_for_NET.pdf) by the 🛡️ [Zcash Foundation](https://twitter.com/ZcashFoundation).

## Nerdbank.Bitcoin

[![NuGet package](https://img.shields.io/nuget/v/Nerdbank.Bitcoin.svg)](https://nuget.org/packages/Nerdbank.Bitcoin)

* BIP-32 Hierarchical Deterministic wallets
* BIP-39 seed phrases
* BIP-44 Multi-Account Hierarchy for Deterministic Wallets
* and more

[Learn more about this package.](https://github.com/nerdcash/Nerdbank.Cryptocurrencies/blob/main/src/Nerdbank.Bitcoin/README.md)
