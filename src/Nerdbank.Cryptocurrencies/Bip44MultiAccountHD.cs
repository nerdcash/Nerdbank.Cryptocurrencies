// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Cryptocurrencies.Bip32HDWallet;
using static Nerdbank.Cryptocurrencies.Bip32HDWallet.KeyPath;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Implements Multi-Account Hierarchy for Deterministic Wallets as defined in
/// <see href="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki">BIP-44</see>.
/// </summary>
public static class Bip44MultiAccountHD
{
	/// <summary>
	/// Creates a key derivation path that conforms to the <see href="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki">BIP-44</see> specification
	/// of <c>m / purpose' / coin_type' / account' / change / address_index</c>.
	/// </summary>
	/// <param name="coinType">
	/// <para>The coin type. The <see cref="HardenedBit"/> will be added to this value if it is not specified here.</para>
	/// <para>See the <see href="https://github.com/satoshilabs/slips/blob/master/slip-0044.md">registration of recognized coin values</see>.</para>
	/// </param>
	/// <param name="account">
	/// <para>This level splits the key space into independent user identities, so the wallet never mixes the coins across different accounts.</para>
	/// <para>Users can use these accounts to organize the funds in the same fashion as bank accounts; for donation purposes (where all addresses are considered public), for saving purposes, for common expenses etc.</para>
	/// <para>Accounts are numbered from index 0 in sequentially increasing manner. This number is used as child index in BIP32 derivation.</para>
	/// <para>Hardened derivation is used at this level. The <see cref="HardenedBit"/> is added automatically if necessary.</para>
	/// <para>Software should prevent a creation of an account if a previous account does not have a transaction history (meaning none of its addresses have been used before).</para>
	/// <para>Software needs to discover all used accounts after importing the seed from an external source. Such an algorithm is described in "Account discovery" chapter.</para>
	/// </param>
	/// <param name="change">
	/// <para>Constant 0 is used for external chain and constant 1 for internal chain (also known as change addresses).
	/// External chain is used for addresses that are meant to be visible outside of the wallet
	/// (e.g. for receiving payments). Internal chain is used for addresses which are not meant to be visible
	/// outside of the wallet and is used for return transaction change.</para>
	/// <para>This number should <em>not</em> include the <see cref="HardenedBit"/>.</para>
	/// </param>
	/// <param name="addressIndex">
	/// <para>The address index. Increment this to get a new receiving address that belongs to the same logical account.</para>
	/// <para>Addresses are numbered from index 0 in sequentially increasing manner. This number is used as child index in BIP32 derivation.</para>
	/// <para>This number should <em>not</em> include the <see cref="HardenedBit"/>.</para>
	/// </param>
	/// <returns>The BIP-44 compliant key path.</returns>
	public static KeyPath CreateKeyPath(uint coinType, uint account, uint change, uint addressIndex)
	{
		// m / purpose' / coin_type' / account' / change / address_index
		return new(addressIndex, new(change, new(account | HardenedBit, new(coinType | HardenedBit, new(44 | HardenedBit)))));
	}
}
