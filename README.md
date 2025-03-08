﻿# Nerdbank cryptocurrency libraries

***.NET libraries for interacting with Zcash and other cryptocurrencies***

[![🏭 Build](https://github.com/nerdcash/Nerdbank.Cryptocurrencies/actions/workflows/build.yml/badge.svg)](https://github.com/nerdcash/Nerdbank.Cryptocurrencies/actions/workflows/build.yml)
[![codecov](https://codecov.io/gh/nerdcash/Nerdbank.Cryptocurrencies/branch/main/graph/badge.svg?token=ATCC7NEXTC)](https://codecov.io/gh/nerdcash/Nerdbank.Cryptocurrencies)

Check out [our docs](doc/index.md) and our features below.

See this library [running live via Blazor in your web browser](https://zcash.nerdbank.net/).

Please be advised of the following:

- 🚫🕵🏻 This code currently is not audited by an external security auditor, use it at your own risk.
- 🚫🕵🏻 The code has not been subjected to thorough review by engineers at the Electric Coin Company or anywhere else.
- 🚫 Sending and receiving funds in the Orchard pool does not yet work. See [librustzcash#404](https://github.com/zcash/librustzcash/issues/404) and [librustzcash#403](https://github.com/zcash/librustzcash/issues/403).
- ⚠️ Transparent funds must be shielded before they can be spent.
- 🚧 We are actively adding features and fixing bugs.

## Nerdbank.Cryptocurrencies

[![NuGet package](https://img.shields.io/nuget/v/Nerdbank.Cryptocurrencies.svg)](https://www.nuget.org/packages/Nerdbank.Cryptocurrencies)

* Cryptography functions that are common to many cryptocurrencies, such as:
  * Base58Check
  * Bech32 and Bech32m
  * Blake2B
  * Compact Size

BIPs, and more...

[Learn more about this package.](src/Nerdbank.Cryptocurrencies/README.md)

## Nerdbank.Zcash

[![NuGet package](https://img.shields.io/nuget/v/Nerdbank.Zcash.svg)](https://www.nuget.org/packages/Nerdbank.Zcash)

* Parse and construct Zcash addresses, including transparent, shielded and unified addresses.
* Lightclient functionality.

[Learn more about this package.](src/Nerdbank.Zcash/README.md)

This project [sponsored in part](https://zfnd.org/wp-content/uploads/2023/04/Unified_Address_library_for_NET.pdf) by the 🛡️ [Zcash Foundation](https://x.com/ZcashFoundation).

## Nerdbank.Bitcoin

[![NuGet package](https://img.shields.io/nuget/v/Nerdbank.Bitcoin.svg)](https://www.nuget.org/packages/Nerdbank.Bitcoin)

* BIP-32 Hierarchical Deterministic wallets
* BIP-39 seed phrases
* BIP-44 Multi-Account Hierarchy for Deterministic Wallets
* and more

[Learn more about this package.](src/Nerdbank.Bitcoin/README.md)

## Donations

Zcash donations are gratefully accepted:
`u1vv2ws6xhs72faugmlrasyeq298l05rrj6wfw8hr3r29y3czev5qt4ugp7kylz6suu04363ze92dfg8ftxf3237js0x9p5r82fgy47xkjnw75tqaevhfh0rnua72hurt22v3w3f7h8yt6mxaa0wpeeh9jcm359ww3rl6fj5ylqqv54uuwrs8q4gys9r3cxdm3yslsh3rt6p7wznzhky7`
