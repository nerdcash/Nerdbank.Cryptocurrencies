# Nerdbank.Zcash

## Features

* Parse and construct Zcash addresses, including transparent, shielded and unified addresses.
* Extract the receivers from unified addresses and others.
* Diversifiable addresses.
* Generate or import Zcash accounts backed by HD wallets or individual keys.

### Light client functionality

* Programmatically receive or spend Zcash.
* Supports Orchard, Sapling and Transparent pools.
* Zcash CLI allows you to download and send transactions with simple commands and no programming.
* User-friendly balances that helps an app to illustrate spendable funds vs. other funds that are held by the user but are not yet available for spending.
* Supports spending and full viewing keys. (Incoming viewing keys coming later.)

### Implemented ZIPs

* ZIP-32 HD Wallets
* ZIP-173 Bech-32 Format
* ZIP-225 Version 5 Transaction Format
* ZIP-302 Memos
* ZIP-321 Payment request URIs
* and more (indirectly, through rust crates that we use)

This project [sponsored in part](https://zfnd.org/wp-content/uploads/2023/04/Unified_Address_library_for_NET.pdf) by the 🛡️ [Zcash Foundation](https://x.com/ZcashFoundation).

## Online live demo

See this library [running live via Blazor in your web browser](https://zcash.nerdbank.net/).

## Sample usage

```csharp
using Nerdbank.Zcash;

// Parse addresses:
var transparent = ZcashAddress.Parse("t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF");
var sapling = ZcashAddress.Parse("zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy");
var unified = ZcashAddress.Parse("u1vv2ws6xhs72faugmlrasyeq298l05rrj6wfw8hr3r29y3czev5qt4ugp7kylz6suu04363ze92dfg8ftxf3237js0x9p5r82fgy47xkjnw75tqaevhfh0rnua72hurt22v3w3f7h8yt6mxaa0wpeeh9jcm359ww3rl6fj5ylqqv54uuwrs8q4gys9r3cxdm3yslsh3rt6p7wznzhky7");

// Extract the receivers from unified addresses:
SaplingAddress? sapling = unified.Receivers.OfType<SaplingAddress>().FirstOrDefault();

// Construct unified addresses:
UnifiedAddress unified = UnifiedAddress.Create(new[]
{
	ZcashAddress.Parse("zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy"),
	ZcashAddress.Parse("t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF"),
});
string myUnified = unified.ToString();
```
