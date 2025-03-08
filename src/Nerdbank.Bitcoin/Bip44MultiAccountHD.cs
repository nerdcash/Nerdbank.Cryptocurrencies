// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Bitcoin;

/// <summary>
/// Implements Multi-Account Hierarchy for Deterministic Wallets as defined in
/// <see href="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki">BIP-44</see>.
/// </summary>
public static class Bip44MultiAccountHD
{
	/// <summary>
	/// The address gap limit that is recommended by BIP-44.
	/// </summary>
	public const uint RecommendedAddressGapLimit = 20;

	/// <summary>
	/// The value of the <c>purpose</c> position in the key path.
	/// </summary>
	private const uint Purpose = 44;

	/// <summary>
	/// Enumerates the constants that are to be used for the <c>change</c> position in the key path.
	/// </summary>
	public enum Change : uint
	{
		/// <summary>
		/// The external chain that is used for generating receiving addresses.
		/// </summary>
		ReceivingAddressChain = 0,

		/// <summary>
		/// The internal chain that is used for generating change addresses (i.e. those addresses that should receive the funds that are <em>not</em> transmitted to another party).
		/// </summary>
		ChangeAddressChain = 1,
	}

	/// <summary>
	/// Creates a key derivation path that conforms to the <see href="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki">BIP-44</see> specification
	/// of <c>m / purpose' / coin_type' / account' / change / address_index</c>.
	/// </summary>
	/// <param name="coinType">
	/// <para>The coin type. The <see cref="Bip32KeyPath.HardenedBit"/> will be added to this value if it is not specified here.</para>
	/// <para>See the <see href="https://github.com/satoshilabs/slips/blob/master/slip-0044.md">registration of recognized coin values</see>.</para>
	/// </param>
	/// <param name="account">
	/// <para>This level splits the key space into independent user identities, so the wallet never mixes the coins across different accounts.</para>
	/// <para>Users can use these accounts to organize the funds in the same fashion as bank accounts; for donation purposes (where all addresses are considered public), for saving purposes, for common expenses etc.</para>
	/// <para>Accounts are numbered from index 0 in sequentially increasing manner. This number is used as child index in BIP32 derivation.</para>
	/// <para>Hardened derivation is used at this level. The <see cref="Bip32KeyPath.HardenedBit"/> is added automatically if necessary.</para>
	/// <para>Software should prevent a creation of an account if a previous account does not have a transaction history (meaning none of its addresses have been used before).</para>
	/// <para>Software needs to discover all used accounts after importing the seed from an external source. Such an algorithm is described in "Account discovery" chapter.</para>
	/// </param>
	/// <param name="change">
	/// <para>Constant 0 is used for external chain and constant 1 for internal chain (also known as change addresses).
	/// External chain is used for addresses that are meant to be visible outside of the wallet
	/// (e.g. for receiving payments). Internal chain is used for addresses which are not meant to be visible
	/// outside of the wallet and is used for return transaction change.</para>
	/// <para>This number should <em>not</em> include the <see cref="Bip32KeyPath.HardenedBit"/>.</para>
	/// </param>
	/// <param name="addressIndex">
	/// <para>The address index. Increment this to get a new receiving address that belongs to the same logical account.</para>
	/// <para>Addresses are numbered from index 0 in sequentially increasing manner. This number is used as child index in BIP32 derivation.</para>
	/// <para>This number should <em>not</em> include the <see cref="Bip32KeyPath.HardenedBit"/>.</para>
	/// </param>
	/// <returns>The BIP-44 compliant key path.</returns>
	public static Bip32KeyPath CreateKeyPath(uint coinType, uint account, Change change, uint addressIndex)
	{
		// m / purpose' / coin_type' / account' / change / address_index
		return new(addressIndex, new((uint)change, CreateKeyPath(coinType, account)));
	}

	/// <summary>
	/// Creates a key derivation path that conforms to the <see href="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki">BIP-44</see> specification
	/// of <c>m / purpose' / coin_type' / account'</c>.
	/// </summary>
	/// <inheritdoc cref="CreateKeyPath(uint, uint, Change, uint)"/>
	public static Bip32KeyPath CreateKeyPath(uint coinType, uint account)
	{
		// m / purpose' / coin_type' / account'
		return new(account | Bip32KeyPath.HardenedBit, new(coinType | Bip32KeyPath.HardenedBit, new(Purpose | Bip32KeyPath.HardenedBit, Bip32KeyPath.Root)));
	}

	/// <summary>
	/// Searches for accounts that have been used.
	/// </summary>
	/// <param name="coinType">The coin type to search.</param>
	/// <param name="discover"><inheritdoc cref="DiscoverUsedAddressesAsync(Bip32KeyPath, Func{Bip32KeyPath, ValueTask{bool}}, uint)" path="/param[@name='discover']" /></param>
	/// <param name="addressGapLimit"><inheritdoc cref="DiscoverUsedAddressesAsync(Bip32KeyPath, Func{Bip32KeyPath, ValueTask{bool}}, uint)" path="/param[@name='addressGapLimit']" /></param>
	/// <returns>
	/// An asynchronous sequence of account-level key paths (i.e. <c>m/44'/coin'/account'</c>) that contain transactions.
	/// </returns>
	public static async IAsyncEnumerable<Bip32KeyPath> DiscoverUsedAccountsAsync(uint coinType, Func<Bip32KeyPath, ValueTask<bool>> discover, uint addressGapLimit = RecommendedAddressGapLimit)
	{
		Requires.NotNull(discover);

		const int AccountGapLimit = 1;
		for (uint accountIndex = 0, consecutiveUnusedAccounts = 1; consecutiveUnusedAccounts <= AccountGapLimit; accountIndex++, consecutiveUnusedAccounts++)
		{
			Bip32KeyPath accountKeyPath = CreateKeyPath(coinType, accountIndex);
			await foreach (Bip32KeyPath usedAddress in DiscoverUsedAddressesAsync(accountKeyPath, discover, addressGapLimit))
			{
				yield return accountKeyPath;

				// Stop looking for funds in this account and search for the next account.
				consecutiveUnusedAccounts = 0;
				break;
			}
		}
	}

	/// <summary>
	/// Searches for addresses that have been used within an account.
	/// </summary>
	/// <param name="account">The account-level key path (i.e. <c>m/44'/coin'/account'</c>) to search.</param>
	/// <param name="discover">
	/// <para>
	/// A callback that will perform the actual search. This callback receives the full key derivation path from which to derive an address and search it for transactions.
	/// It should return a value indicating whether the address has <em>ever</em> received funds, which serves as feedback into the search to direct it to continue or stop.
	/// </para>
	/// <para>
	/// This delegate may be invoked concurrently from multiple threads. Care should be taken to ensure that any side-effects are thread-safe.
	/// </para>
	/// <para>
	/// Exceptions thrown from this delegate will abort the search and be allowed to propagate outside this method.
	/// </para>
	/// </param>
	/// <param name="addressGapLimit">
	/// The number of consecutively unused addresses to search before aborting the search in one particular chain.
	/// The <see cref="RecommendedAddressGapLimit">default value</see> is the one recommended by BIP-44, and is a good value to use because wallet software should have
	/// warned the user about generating a new address that would create a gap beyond this limit.
	/// </param>
	/// <returns>
	/// An asynchronous sequence of full key paths (i.e. <c>m/44'/coin'/account'/change/addressIndex</c>) that contain transactions.
	/// </returns>
	public static async IAsyncEnumerable<Bip32KeyPath> DiscoverUsedAddressesAsync(Bip32KeyPath account, Func<Bip32KeyPath, ValueTask<bool>> discover, uint addressGapLimit = RecommendedAddressGapLimit)
	{
		Requires.NotNull(account);
		Requires.Argument(account.Length == 3 && account[1] == (Purpose | Bip32KeyPath.HardenedBit), nameof(account), "This is not an account-level BIP-44 key derivation path.");
		Requires.NotNull(discover);

		// Always search the external chain, as that is where receiving addresses come from.
		bool foundAny = false;
		await foreach (Bip32KeyPath usedAddress in SearchChainAsync(new((uint)Change.ReceivingAddressChain, account)))
		{
			yield return usedAddress;
			foundAny = true;
		}

		// If the external chain was used, then the internal "change" chain may have been used as well.
		if (foundAny)
		{
			await foreach (Bip32KeyPath usedAddress in SearchChainAsync(new((uint)Change.ChangeAddressChain, account)))
			{
				yield return usedAddress;
				foundAny = true;
			}
		}

		async IAsyncEnumerable<Bip32KeyPath> SearchChainAsync(Bip32KeyPath chain)
		{
			// This loops over the address indexes, incrementing the index each time, until it encounters too many unused indexes in a row.
			for (uint addressIndex = 0, consecutiveUnusedAddresses = 1; consecutiveUnusedAddresses <= addressGapLimit; addressIndex++, consecutiveUnusedAddresses++)
			{
				Bip32KeyPath keyPath = new(addressIndex, chain);
				if (await discover(keyPath).ConfigureAwait(false))
				{
					consecutiveUnusedAddresses = 0;
					yield return keyPath;
				}
			}
		}
	}
}
