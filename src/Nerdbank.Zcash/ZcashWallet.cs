// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using static Nerdbank.Zcash.Zip32HDWallet;

namespace Nerdbank.Zcash;

/// <summary>
/// A high-level API for creating, managing and using Zcash accounts.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class ZcashWallet
{
	private readonly Zip32HDWallet zip32;
	private readonly SortedDictionary<uint, Account> accounts = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashWallet"/> class.
	/// </summary>
	/// <param name="mnemonic">The mnemonic that will always initialize the same wallet with access to the same funds.</param>
	/// <param name="network">The network the wallet operates on.</param>
	public ZcashWallet(Bip39Mnemonic mnemonic, ZcashNetwork network)
	{
		this.zip32 = new Zip32HDWallet(mnemonic, network);
		this.AddAccount();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashWallet"/> class.
	/// </summary>
	/// <param name="seed">The seed to use as the master keys, from which accounts are derived. The same seed will always generate the same accounts.</param>
	/// <param name="network">The network this wallet should be used on.</param>
	public ZcashWallet(ReadOnlySpan<byte> seed, ZcashNetwork network)
	{
		this.zip32 = new Zip32HDWallet(seed, network);
		this.AddAccount();
	}

	/// <inheritdoc cref="Zip32HDWallet.Network"/>
	public ZcashNetwork Network => this.zip32.Network;

	/// <inheritdoc cref="Zip32HDWallet.Mnemonic"/>
	public Bip39Mnemonic? Mnemonic => this.zip32.Mnemonic;

	/// <inheritdoc cref="Zip32HDWallet.Seed"/>
	public ReadOnlyMemory<byte> Seed => this.zip32.Seed;

	/// <summary>
	/// Gets the dictionary of accounts, keyed by their index.
	/// </summary>
	/// <remarks>
	/// Enumerating the dictionary will always produce the accounts in order of their index.
	/// </remarks>
	public IReadOnlyDictionary<uint, Account> Accounts => this.accounts;

	private string DebuggerDisplay => $"Zcash wallet: {this.zip32.Mnemonic?.ToString(2)}";

	/// <summary>
	/// Adds a new account to the wallet.
	/// </summary>
	/// <param name="index">The index of the account to create. For a given wallet (mnemonic), a given index will always recreate the same account with access to the same funds.</param>
	/// <returns>The newly created account.</returns>
	/// <remarks>
	/// The new account will be added to the <see cref="Accounts"/> collection.
	/// </remarks>
	public Account AddAccount(uint? index = null)
	{
		if (index is null)
		{
			for (index = 0; index < uint.MaxValue && this.accounts.ContainsKey(index.Value); index++)
			{
			}
		}
		else
		{
			Verify.Operation(!this.accounts.ContainsKey(index.Value), "The account with that index already exists.");
		}

		Account account = new(this, index.Value);
		this.accounts.Add(index.Value, account);
		return account;
	}

	/// <summary>
	/// Describes a single Zcash account, with unique spending authorities.
	/// </summary>
	[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
	public class Account
	{
		/// <summary>
		/// The wallet that owns this account.
		/// </summary>
		private readonly ZcashWallet owner;

		/// <summary>
		/// The number of transparent addresses that are likely to have been generated.
		/// </summary>
		/// <remarks>
		/// TODO: This number should be generated from a call to <see cref="Bip44MultiAccountHD.DiscoverUsedAccountsAsync(uint, Func{Bip32HDWallet.KeyPath, ValueTask{bool}}, uint)"/>.
		/// </remarks>
		private uint transparentAddressesToScanAsync = Bip44MultiAccountHD.RecommendedAddressGapLimit;

		/// <summary>
		/// Initializes a new instance of the <see cref="Account"/> class.
		/// </summary>
		/// <param name="owner">The owning wallet.</param>
		/// <param name="index">The account index within the wallet.</param>
		internal Account(ZcashWallet owner, uint index)
		{
			this.owner = owner;
			this.Index = index;

			this.Transparent = owner.zip32.CreateTransparentAccount(index);
			this.Sapling = owner.zip32.CreateSaplingAccount(index);
			this.Orchard = owner.zip32.CreateOrchardAccount(index);

			this.DefaultAddress = UnifiedAddress.Create(
				this.Transparent.DefaultAddress,
				this.Sapling.DefaultAddress,
				this.Orchard.DefaultAddress);
			this.FullViewingKey = UnifiedViewingKey.Full.Create(
				this.Transparent.FullViewingKey,
				this.Sapling.FullViewingKey,
				this.Orchard.FullViewingKey);
			this.SpendingKey = UnifiedSpendingKey.Create(
				this.Transparent,
				this.Sapling,
				this.Orchard.SpendingKey);
		}

		/// <summary>
		/// Gets the derivation index of this account.
		/// </summary>
		/// <remarks>
		/// This is typically 0 for the first account, with increasing indexes for each additional account created from the same wallet/mnemonic.
		/// </remarks>
		public uint Index { get; }

		/// <summary>
		/// Gets the spending key for the transparent pool (<c>m/44'/133'/account'</c>).
		/// </summary>
		public Transparent.ExtendedSpendingKey Transparent { get; }

		/// <summary>
		/// Gets the spending key for the sapling pool.
		/// </summary>
		public Zip32HDWallet.Sapling.ExtendedSpendingKey Sapling { get; }

		/// <summary>
		/// Gets the spending key for the orchard pool.
		/// </summary>
		public Zip32HDWallet.Orchard.ExtendedSpendingKey Orchard { get; }

		/// <summary>
		/// Gets the default unified address for this account.
		/// </summary>
		public UnifiedAddress DefaultAddress { get; }

		/// <summary>
		/// Gets the full viewing key for this account.
		/// </summary>
		public UnifiedViewingKey.Full FullViewingKey { get; }

		/// <summary>
		/// Gets the incoming viewing key for this account.
		/// </summary>
		public UnifiedViewingKey.Incoming IncomingViewingKey => this.FullViewingKey.IncomingViewingKey;

		/// <summary>
		/// Gets the unified spending key for this account.
		/// </summary>
		internal UnifiedSpendingKey SpendingKey { get; }

		private string DebuggerDisplay => $"{this.Index}: {this.DefaultAddress}";

		/// <summary>
		/// Gets a diversifier index that is unique to this moment in time,
		/// for use as an input to the <see cref="GetDiversifiedAddress(ref DiversifierIndex)"/> method.
		/// </summary>
		/// <returns>The diversifier index.</returns>
		public static DiversifierIndex GetTimeBasedDiversifier() => new(DateTime.UtcNow.Ticks);

		/// <inheritdoc cref="GetDiversifiedAddress(ref DiversifierIndex)"/>
		public UnifiedAddress GetDiversifiedAddress()
		{
			DiversifierIndex index = GetTimeBasedDiversifier();
			return this.GetDiversifiedAddress(ref index);
		}

		/// <summary>
		/// Gets a unique address that sends ZEC to this account but cannot be correlated with other addresses
		/// except using the <see cref="IncomingViewingKey"/>.
		/// </summary>
		/// <param name="diversifierIndex">
		/// The 11-byte diversifier index to start searching at, in the range of 0..(2^88 - 1).
		/// Not every index will produce a valid diversifier. About half will fail.
		/// The default diversifier is defined as the smallest non-negative index that produces a valid diversifier.
		/// This value will be incremented until a diversifier can be found, considering the buffer to be a little-endian encoded integer.
		/// </param>
		/// <returns>
		/// The diversified address. This will <em>not</em> include a transparent pool receiver because it does not support diversifiers.
		/// </returns>
		/// <remarks>
		/// By giving a unique diversified address to each person that sends you ZEC,
		/// you prevent them from being able to correlate this address with others to discover they are
		/// interacting with the same person, thereby increasing your privacy.
		/// </remarks>
		public UnifiedAddress GetDiversifiedAddress(ref DiversifierIndex diversifierIndex)
		{
			// Now get the closest matching Sapling diversifier we can.
			Verify.Operation(this.Sapling.IncomingViewingKey.TryCreateReceiver(ref diversifierIndex, out SaplingReceiver? sapling), "No sapling diversifier could be found at or above that index.");

			// The orchard diversifier always works.
			OrchardReceiver orchard = this.Orchard.SpendingKey.FullViewingKey.IncomingViewingKey.CreateReceiver(diversifierIndex);

			return UnifiedAddress.Create(
				new SaplingAddress(sapling.Value, this.owner.Network),
				new OrchardAddress(orchard, this.owner.Network));
		}

		/// <summary>
		/// Checks whether a given address sends ZEC to this account.
		/// </summary>
		/// <param name="address">The address to test.</param>
		/// <returns><see langword="true" /> if all receivers in <paramref name="address"/> are confirmed to direct ZEC to this account; <see langword="false" /> otherwise.</returns>
		/// <remarks>
		/// There is a risk that a unified address containing multiple receivers may have been
		/// contrived to include receivers from this account and other receivers <em>not</em> belonging to this account.
		/// To avoid being tricked into reusing such a contrived address and unwittingly diverting ZEC to someone else's wallet,
		/// <see langword="false"/> is returned if any receiver does not belong to this account.
		/// </remarks>
		public bool AddressSendsToThisAcount(ZcashAddress address)
		{
			Requires.NotNull(address);

			if (address is UnifiedAddress ua)
			{
				if (ua.Receivers.Count == 0)
				{
					return false;
				}

				foreach (ZcashAddress individualAddress in ua.Receivers)
				{
					if (!TestAddress(individualAddress))
					{
						return false;
					}
				}

				return true;
			}
			else
			{
				return TestAddress(address);
			}

			bool TestAddress(ZcashAddress individualAddress)
			{
				return
					(individualAddress.GetPoolReceiver<OrchardReceiver>() is { } orchardReceiver && this.Orchard.IncomingViewingKey.CheckReceiver(orchardReceiver)) ||
					(individualAddress.GetPoolReceiver<SaplingReceiver>() is { } saplingReceiver && this.Sapling.IncomingViewingKey.CheckReceiver(saplingReceiver)) ||
					(individualAddress.GetPoolReceiver<TransparentP2PKHReceiver>() is { } p2pkhReceiver && this.Transparent.FullViewingKey.CheckReceiver(p2pkhReceiver, this.transparentAddressesToScanAsync)) ||
					(individualAddress.GetPoolReceiver<TransparentP2SHReceiver>() is { } p2shReceiver && this.Transparent.FullViewingKey.CheckReceiver(p2shReceiver, this.transparentAddressesToScanAsync));
			}
		}
	}
}
