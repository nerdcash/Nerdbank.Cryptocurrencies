// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Bitcoin;

/// <summary>
/// Implements Hierarchical Deterministic Wallets as defined in
/// <see href="https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki">BIP-32</see>.
/// </summary>
public static partial class Bip32HDWallet
{
	private const int PublicKeyLength = 33;
}
