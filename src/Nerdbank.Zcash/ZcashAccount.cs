// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Zcash.Zip32HDWallet;

namespace Nerdbank.Zcash;

/// <summary>
/// Describes a single Zcash account, with keys for one or more Zcash pools.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class ZcashAccount
{
	/// <summary>
	/// The number of transparent addresses that are likely to have been generated.
	/// </summary>
	/// <remarks>
	/// TODO: This number should be generated from a call to <see cref="Bip44MultiAccountHD.DiscoverUsedAccountsAsync(uint, Func{Bip32HDWallet.KeyPath, ValueTask{bool}}, uint)"/>.
	/// </remarks>
	private uint transparentAddressesToScanAsync = Bip44MultiAccountHD.RecommendedAddressGapLimit;

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashAccount"/> class
	/// with spending capability.
	/// </summary>
	/// <param name="zip32">The master keys by which to derive the various pool keys.</param>
	/// <param name="index">The account index within the wallet.</param>
	public ZcashAccount(Zip32HDWallet zip32, uint index = 0)
	{
		Requires.NotNull(zip32);

		Transparent.ExtendedSpendingKey transparent = zip32.CreateTransparentAccount(index);
		Zip32HDWallet.Sapling.ExtendedSpendingKey sapling = zip32.CreateSaplingAccount(index);
		Zip32HDWallet.Orchard.ExtendedSpendingKey orchard = zip32.CreateOrchardAccount(index);

		this.Spending = new SpendingKeys(transparent, sapling, orchard);
		this.FullViewing = this.Spending.FullViewingKey;
		this.IncomingViewing = this.FullViewing.IncomingViewingKey;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashAccount"/> class
	/// that can view but not create transactions.
	/// </summary>
	/// <param name="viewingKey">The viewing key. This may be a full or incoming viewing key.</param>
	public ZcashAccount(UnifiedViewingKey viewingKey)
	{
		Requires.NotNull(viewingKey);

		if (viewingKey is UnifiedViewingKey.Full)
		{
			this.FullViewing = new FullViewingKeys(
				viewingKey.GetViewingKey<Transparent.ExtendedViewingKey>(),
				viewingKey.GetViewingKey<Sapling.DiversifiableFullViewingKey>(),
				viewingKey.GetViewingKey<Orchard.FullViewingKey>());

			this.IncomingViewing = this.FullViewing.IncomingViewingKey;
		}
		else
		{
			this.IncomingViewing = new IncomingViewingKeys(
				viewingKey.GetViewingKey<Transparent.ExtendedViewingKey>(),
				viewingKey.GetViewingKey<Sapling.IncomingViewingKey>(),
				viewingKey.GetViewingKey<Orchard.IncomingViewingKey>());
		}
	}

	private ZcashAccount(SpendingKeys keys)
	{
		Requires.NotNull(keys);

		this.Spending = keys;
		this.FullViewing = this.Spending.FullViewingKey;
		this.IncomingViewing = this.FullViewing.IncomingViewingKey;
	}

	private ZcashAccount(FullViewingKeys keys)
	{
		Requires.NotNull(keys);

		this.FullViewing = keys;
		this.IncomingViewing = this.FullViewing.IncomingViewingKey;
	}

	private ZcashAccount(IncomingViewingKeys keys)
	{
		Requires.NotNull(keys);

		this.IncomingViewing = keys;
	}

	/// <summary>
	/// Gets the network this account should be used with.
	/// </summary>
	public ZcashNetwork Network => this.IncomingViewing.UnifiedKey.Network;

	/// <summary>
	/// Gets the spending keys, if this account has access to them.
	/// </summary>
	public SpendingKeys? Spending { get; }

	/// <summary>
	/// Gets the full viewing keys, if this account has access to them.
	/// </summary>
	public FullViewingKeys? FullViewing { get; }

	/// <summary>
	/// Gets the incoming viewing keys.
	/// </summary>
	public IncomingViewingKeys IncomingViewing { get; }

	/// <summary>
	/// Gets the birthday height on this account.
	/// </summary>
	public ulong? BirthdayHeight { get; init; }

	/// <summary>
	/// Gets the default unified address for this account.
	/// </summary>
	public UnifiedAddress DefaultAddress => this.IncomingViewing.UnifiedKey.DefaultAddress;

	/// <summary>
	/// Gets a value indicating whether this account contains an orchard or sapling key.
	/// </summary>
	public bool HasDiversifiableKeys => this.IncomingViewing.Orchard is not null || this.IncomingViewing.Sapling is not null;

	private string DebuggerDisplay => $"{this.DefaultAddress}";

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashAccount"/> class
	/// from the encoded form of one or more keys.
	/// </summary>
	/// <param name="encodedKey">The standard encoding of some key.</param>
	/// <param name="account">Receives the initialized account, if parsing is successful.</param>
	/// <returns><see langword="true" /> if <paramref name="encodedKey"/> was recognized as an encoding of some Zcash-related key; <see langword="false" /> otherwise.</returns>
	public static bool TryImportAccount(string encodedKey, [NotNullWhen(true)] out ZcashAccount? account)
	{
		account = null;
		if (ZcashUtilities.TryParseKey(encodedKey, out IKeyWithTextEncoding? result))
		{
			if (result is UnifiedViewingKey unifiedViewingKey)
			{
				account = new ZcashAccount(unifiedViewingKey);
			}
			else if (result is ISpendingKey && result is Zip32HDWallet.IExtendedKey extendedSK)
			{
				account = new ZcashAccount(SpendingKeys.FromKeys(extendedSK));
			}
			else if (result is IFullViewingKey fvk)
			{
				account = new ZcashAccount(FullViewingKeys.FromKeys(fvk));
			}
			else if (result is IIncomingViewingKey ivk)
			{
				account = new ZcashAccount(IncomingViewingKeys.FromKeys(ivk));
			}
		}

		return account is not null;
	}

	/// <inheritdoc cref="GetDiversifiedAddress(ref DiversifierIndex)"/>
	public UnifiedAddress GetDiversifiedAddress()
	{
		DiversifierIndex index = GetTimeBasedDiversifier();
		return this.GetDiversifiedAddress(ref index);
	}

	/// <summary>
	/// Gets a unique address that sends ZEC to this account but cannot be correlated with other addresses
	/// except using the <see cref="IncomingViewing"/> key.
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
	/// <exception cref="InvalidOperationException">
	/// Thrown if this account has no diversifiable keys.
	/// This can be predicted using <see cref="HasDiversifiableKeys"/>.
	/// </exception>
	public UnifiedAddress GetDiversifiedAddress(ref DiversifierIndex diversifierIndex)
	{
		List<ZcashAddress> componentAddresses = new(2);

		// Now get the closest matching Sapling diversifier we can.
		if (this.IncomingViewing.Sapling is not null)
		{
			Verify.Operation(this.IncomingViewing.Sapling.TryCreateReceiver(ref diversifierIndex, out SaplingReceiver? sapling), "No sapling diversifier could be found at or above that index.");
			componentAddresses.Add(new SaplingAddress(sapling.Value, this.Network));
		}

		// The orchard diversifier always works.
		if (this.IncomingViewing.Orchard is not null)
		{
			OrchardReceiver orchard = this.IncomingViewing.Orchard.CreateReceiver(diversifierIndex);
			componentAddresses.Add(new OrchardAddress(orchard, this.Network));
		}

		Verify.Operation(componentAddresses.Count > 0, "This account doesn't include any diversifiable keys.");

		return UnifiedAddress.Create(componentAddresses);
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
			if (this.IncomingViewing.Orchard is not null && individualAddress.GetPoolReceiver<OrchardReceiver>() is { } orchardReceiver && this.IncomingViewing.Orchard.CheckReceiver(orchardReceiver))
			{
				return true;
			}

			if (this.IncomingViewing.Sapling is not null && individualAddress.GetPoolReceiver<SaplingReceiver>() is { } saplingReceiver && this.IncomingViewing.Sapling.CheckReceiver(saplingReceiver))
			{
				return true;
			}

			Transparent.ExtendedViewingKey? transparentViewing = this.FullViewing?.Transparent ?? this.IncomingViewing.Transparent;
			if (transparentViewing is not null)
			{
				if (individualAddress.GetPoolReceiver<TransparentP2PKHReceiver>() is { } p2pkhReceiver && transparentViewing.CheckReceiver(p2pkhReceiver, this.transparentAddressesToScanAsync))
				{
					return true;
				}

				if (individualAddress.GetPoolReceiver<TransparentP2SHReceiver>() is { } p2shReceiver && transparentViewing.CheckReceiver(p2shReceiver, this.transparentAddressesToScanAsync))
				{
					return true;
				}
			}

			return false;
		}
	}

	/// <summary>
	/// Gets a diversifier index that is unique to this moment in time,
	/// for use as an input to the <see cref="GetDiversifiedAddress(ref DiversifierIndex)"/> method.
	/// </summary>
	/// <returns>The diversifier index.</returns>
	private static DiversifierIndex GetTimeBasedDiversifier() => new(DateTime.UtcNow.Ticks);

	/// <summary>
	/// Spending keys for each pool.
	/// </summary>
	public record SpendingKeys : ISpendingKey
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SpendingKeys"/> class.
		/// </summary>
		/// <param name="transparent">A key for the transparent pool.</param>
		/// <param name="sapling">A key for the sapling pool.</param>
		/// <param name="orchard">A key for the orchard pool.</param>
		internal SpendingKeys(
			Transparent.ExtendedSpendingKey? transparent,
			Zip32HDWallet.Sapling.ExtendedSpendingKey? sapling,
			Zip32HDWallet.Orchard.ExtendedSpendingKey? orchard)
		{
			this.Transparent = transparent;
			this.Sapling = sapling;
			this.Orchard = orchard;
			this.UnifiedKey = UnifiedSpendingKey.Create(ZcashUtilities.RemoveNulls<ISpendingKey>(
				transparent,
				sapling,
				orchard?.SpendingKey));

			this.FullViewingKey = new FullViewingKeys(
				transparent?.FullViewingKey,
				sapling?.FullViewingKey,
				orchard?.FullViewingKey);

			this.Internal = new(this);
		}

		/// <summary>
		/// Gets the unified spending key for this account.
		/// </summary>
		internal UnifiedSpendingKey UnifiedKey { get; }

		/// <summary>
		/// Gets the spending key for the transparent pool (<c>m/44'/133'/account'</c>).
		/// </summary>
		public Transparent.ExtendedSpendingKey? Transparent { get; }

		/// <summary>
		/// Gets the spending key for the sapling pool.
		/// </summary>
		public Zip32HDWallet.Sapling.ExtendedSpendingKey? Sapling { get; }

		/// <summary>
		/// Gets the spending key for the orchard pool.
		/// </summary>
		public Zip32HDWallet.Orchard.ExtendedSpendingKey? Orchard { get; }

		/// <summary>
		/// Gets the spending keys for the internal addresses used for change and shielding.
		/// </summary>
		public InternalSpendingKeys Internal { get; }

		/// <summary>
		/// Gets the full viewing key.
		/// </summary>
		internal FullViewingKeys FullViewingKey { get; }

		/// <inheritdoc/>
		IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

		/// <summary>
		/// Initializes a new instance of the <see cref="SpendingKeys"/> class.
		/// </summary>
		/// <param name="spendingKeys">An array of keys that may be included in this instance.</param>
		/// <returns>The newly created object.</returns>
		/// <exception cref="NotSupportedException">Thrown if any of the elements of <paramref name="spendingKeys"/> is not supported.</exception>
		internal static SpendingKeys FromKeys(params Zip32HDWallet.IExtendedKey[] spendingKeys)
		{
			Transparent.ExtendedSpendingKey? transparent = null;
			Zip32HDWallet.Sapling.ExtendedSpendingKey? sapling = null;
			Zip32HDWallet.Orchard.ExtendedSpendingKey? orchard = null;

			foreach (Zip32HDWallet.IExtendedKey key in spendingKeys)
			{
				if (key is Transparent.ExtendedSpendingKey t)
				{
					transparent = t;
				}
				else if (key is Zip32HDWallet.Sapling.ExtendedSpendingKey s)
				{
					sapling = s;
				}
				else if (key is Zip32HDWallet.Orchard.ExtendedSpendingKey o)
				{
					orchard = o;
				}
				else
				{
					throw new NotSupportedException($"Unsupported key type: {key.GetType()}");
				}
			}

			return new(transparent, sapling, orchard);
		}
	}

	/// <summary>
	/// Spending keys for the internal addresses.
	/// </summary>
	/// <remarks>
	/// There is no orchard key in this class because for orchard, the public address spending key implicitly
	/// has spending authority over the internal address.
	/// </remarks>
	public record InternalSpendingKeys
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InternalSpendingKeys"/> class.
		/// </summary>
		/// <param name="spendingKeys">The public spending keys from which to derive the internal keys.</param>
		internal InternalSpendingKeys(SpendingKeys spendingKeys)
		{
			this.Sapling = spendingKeys.Sapling?.DeriveInternal().ExpandedSpendingKey;
		}

		/// <summary>
		/// Gets the key for the sapling pool.
		/// </summary>
		public Sapling.ExpandedSpendingKey? Sapling { get; }
	}

	/// <summary>
	/// Full viewing keys for each pool.
	/// </summary>
	public record FullViewingKeys : IFullViewingKey
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FullViewingKeys"/> class.
		/// </summary>
		/// <param name="transparent">A key for the transparent pool.</param>
		/// <param name="sapling">A key for the sapling pool.</param>
		/// <param name="orchard">A key for the orchard pool.</param>
		internal FullViewingKeys(Transparent.ExtendedViewingKey? transparent, Sapling.DiversifiableFullViewingKey? sapling, Orchard.FullViewingKey? orchard)
		{
			this.Transparent = transparent;
			this.Sapling = sapling;
			this.Orchard = orchard;

			this.UnifiedKey = UnifiedViewingKey.Full.Create(ZcashUtilities.RemoveNulls<IFullViewingKey>(
				transparent,
				sapling,
				orchard));

			this.IncomingViewingKey = new IncomingViewingKeys(
				transparent?.IncomingViewingKey,
				sapling?.IncomingViewingKey,
				orchard?.IncomingViewingKey);

			this.Internal = new(this);
		}

		/// <summary>
		/// Gets the unified full viewing key for this account.
		/// </summary>
		public UnifiedViewingKey.Full UnifiedKey { get; }

		/// <summary>
		/// Gets the full viewing key for the transparent pool (<c>m/44'/133'/account'</c>).
		/// </summary>
		public Transparent.ExtendedViewingKey? Transparent { get; }

		/// <summary>
		/// Gets the full viewing key for the sapling pool.
		/// </summary>
		public Sapling.DiversifiableFullViewingKey? Sapling { get; }

		/// <summary>
		/// Gets the full viewing key for the orchard pool.
		/// </summary>
		public Orchard.FullViewingKey? Orchard { get; }

		/// <summary>
		/// Gets the full viewing keys for the internal addresses used for change and shielding.
		/// </summary>
		public InternalFullViewingKeys Internal { get; }

		/// <summary>
		/// Gets the incoming viewing key.
		/// </summary>
		internal IncomingViewingKeys IncomingViewingKey { get; }

		/// <inheritdoc/>
		IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

		/// <summary>
		/// Initializes a new instance of the <see cref="FullViewingKeys"/> class.
		/// </summary>
		/// <param name="fullViewingKeys">An array of keys that may be included in this instance.</param>
		/// <returns>The newly created object.</returns>
		/// <exception cref="NotSupportedException">Thrown if any of the elements of <paramref name="fullViewingKeys"/> is not supported.</exception>
		internal static FullViewingKeys FromKeys(params IFullViewingKey[] fullViewingKeys)
		{
			Transparent.ExtendedViewingKey? transparent = null;
			Sapling.DiversifiableFullViewingKey? sapling = null;
			Orchard.FullViewingKey? orchard = null;

			foreach (Zip32HDWallet.IExtendedKey key in fullViewingKeys)
			{
				if (key is Transparent.ExtendedViewingKey t)
				{
					transparent = t;
				}
				else if (key is Sapling.DiversifiableFullViewingKey s)
				{
					sapling = s;
				}
				else if (key is Zip32HDWallet.Sapling.ExtendedFullViewingKey extSapling)
				{
					sapling = extSapling.FullViewingKey;
				}
				else if (key is Orchard.FullViewingKey o)
				{
					orchard = o;
				}
				else
				{
					throw new NotSupportedException($"Unsupported key type: {key.GetType()}");
				}
			}

			return new(transparent, sapling, orchard);
		}
	}

	/// <summary>
	/// Full viewing keys for the internal (change and shielding) addresses.
	/// </summary>
	public record InternalFullViewingKeys
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InternalFullViewingKeys"/> class.
		/// </summary>
		/// <param name="fvk">The public viewing keys from which to derive the internal ones.</param>
		internal InternalFullViewingKeys(FullViewingKeys fvk)
		{
			this.Sapling = fvk.Sapling?.DeriveInternal();
			this.Orchard = fvk.Orchard?.DeriveInternal();
		}

		/// <summary>
		/// Gets the sapling viewing key.
		/// </summary>
		public Sapling.DiversifiableFullViewingKey? Sapling { get; }

		/// <summary>
		/// Gets the orchard viewing key.
		/// </summary>
		public Orchard.FullViewingKey? Orchard { get; }
	}

	/// <summary>
	/// Incoming viewing keys for each pool.
	/// </summary>
	public record IncomingViewingKeys : IIncomingViewingKey
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="IncomingViewingKeys"/> class.
		/// </summary>
		/// <param name="transparent">A key for the transparent pool.</param>
		/// <param name="sapling">A key for the sapling pool.</param>
		/// <param name="orchard">A key for the orchard pool.</param>
		internal IncomingViewingKeys(Transparent.ExtendedViewingKey? transparent, Sapling.IncomingViewingKey? sapling, Orchard.IncomingViewingKey? orchard)
		{
			this.Transparent = transparent;
			this.Sapling = sapling;
			this.Orchard = orchard;
			this.UnifiedKey = UnifiedViewingKey.Incoming.Create(ZcashUtilities.RemoveNulls<IIncomingViewingKey>(
				transparent,
				sapling,
				orchard));
		}

		/// <summary>
		/// Gets the unified incoming viewing key for this account.
		/// </summary>
		public UnifiedViewingKey.Incoming UnifiedKey { get; }

		/// <summary>
		/// Gets the incoming viewing key for the transparent pool (<c>m/44'/133'/account'</c>).
		/// </summary>
		public Transparent.ExtendedViewingKey? Transparent { get; }

		/// <summary>
		/// Gets the incoming viewing key for the sapling pool.
		/// </summary>
		public Sapling.IncomingViewingKey? Sapling { get; }

		/// <summary>
		/// Gets the incoming viewing key for the orchard pool.
		/// </summary>
		public Orchard.IncomingViewingKey? Orchard { get; }

		/// <inheritdoc/>
		ZcashAddress IIncomingViewingKey.DefaultAddress => this.UnifiedKey.DefaultAddress;

		/// <inheritdoc/>
		ZcashNetwork IZcashKey.Network => this.UnifiedKey.Network;

		/// <summary>
		/// Initializes a new instance of the <see cref="IncomingViewingKeys"/> class.
		/// </summary>
		/// <param name="incomingViewingKeys">An array of spending keys that may be included in this instance.</param>
		/// <returns>The newly created object.</returns>
		/// <exception cref="NotSupportedException">Thrown if any of the elements of <paramref name="incomingViewingKeys"/> is not supported.</exception>
		internal static IncomingViewingKeys FromKeys(params IIncomingViewingKey[] incomingViewingKeys)
		{
			Transparent.ExtendedViewingKey? transparent = null;
			Sapling.IncomingViewingKey? sapling = null;
			Orchard.IncomingViewingKey? orchard = null;

			foreach (IIncomingViewingKey key in incomingViewingKeys)
			{
				if (key is Transparent.ExtendedViewingKey t)
				{
					transparent = t;
				}
				else if (key is Sapling.IncomingViewingKey s)
				{
					sapling = s;
				}
				else if (key is Orchard.IncomingViewingKey o)
				{
					orchard = o;
				}
				else
				{
					throw new NotSupportedException($"Unsupported key type: {key.GetType()}");
				}
			}

			return new(transparent, sapling, orchard);
		}
	}
}
