// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Nerdbank.Bitcoin;

namespace Nerdbank.Zcash;

/// <summary>
/// Describes a single Zcash account, with keys for one or more Zcash pools.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class ZcashAccount : INotifyPropertyChanged
{
	/// <summary>
	/// The number of transparent addresses that are likely to have been generated.
	/// </summary>
	/// <remarks>
	/// TODO: This number should be generated from a call to <see cref="Bip44MultiAccountHD.DiscoverUsedAccountsAsync(uint, Func{Bip32KeyPath, ValueTask{bool}}, uint)"/>.
	/// </remarks>
	private uint transparentAddressesToScanAsync = Bip44MultiAccountHD.RecommendedAddressGapLimit;
	private uint? birthdayHeight;
	private uint? maxTransparentAddressIndex;

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashAccount"/> class
	/// with spending capability.
	/// </summary>
	/// <param name="zip32">The master keys by which to derive the various pool keys.</param>
	/// <param name="index">The account index within the wallet.</param>
	public ZcashAccount(Zip32HDWallet zip32, uint index = 0)
	{
		Requires.NotNull(zip32);

		this.HDDerivation = new(zip32, index);

		Zip32HDWallet.Transparent.ExtendedSpendingKey transparent = zip32.CreateTransparentAccount(index);
		Zip32HDWallet.Sapling.ExtendedSpendingKey sapling = zip32.CreateSaplingAccount(index);
		Zip32HDWallet.Orchard.ExtendedSpendingKey orchard = zip32.CreateOrchardAccount(index);

		this.Spending = new SpendingKeys(transparent, sapling, orchard);
		this.FullViewing = this.Spending.FullViewingKey;
		this.IncomingViewing = this.FullViewing.IncomingViewingKey;

		this.MaxTransparentAddressIndex = 0;
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
				viewingKey.GetViewingKey<Zip32HDWallet.Transparent.ExtendedViewingKey>(),
				viewingKey.GetViewingKey<Sapling.DiversifiableFullViewingKey>(),
				viewingKey.GetViewingKey<Orchard.FullViewingKey>());

			this.IncomingViewing = this.FullViewing.IncomingViewingKey;
		}
		else
		{
			this.IncomingViewing = new IncomingViewingKeys(
				viewingKey.GetViewingKey<Zip32HDWallet.Transparent.ExtendedViewingKey>(),
				viewingKey.GetViewingKey<Sapling.DiversifiableIncomingViewingKey>(),
				viewingKey.GetViewingKey<Orchard.IncomingViewingKey>());
		}

		if (this.IncomingViewing.Transparent is not null)
		{
			this.MaxTransparentAddressIndex = 0;
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

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// Gets the ZIP-32 HD wallet and index that was used to generate this account, if applicable.
	/// </summary>
	public HDDerivationSource? HDDerivation { get; }

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
	/// Gets or sets the birthday height on this account.
	/// </summary>
	public uint? BirthdayHeight
	{
		get => this.birthdayHeight;
		set => this.SetPropertyIfChanged(ref this.birthdayHeight, value);
	}

	/// <summary>
	/// Gets or sets a human-readable name for the account.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets the default unified address for this account.
	/// </summary>
	public UnifiedAddress DefaultAddress => this.IncomingViewing.UnifiedKey.DefaultAddress;

	/// <summary>
	/// Gets a value indicating whether this account contains an orchard or sapling key.
	/// </summary>
	public bool HasDiversifiableKeys => this.IncomingViewing.Orchard is not null || this.IncomingViewing.Sapling is not null;

	/// <summary>
	/// Gets or sets the maximum index for a transparent address that is likely to have been generated.
	/// </summary>
	/// <remarks>
	/// This value is useful for scanning for UTXOs, as well as for generating new transparent addresses.
	/// </remarks>
	public uint? MaxTransparentAddressIndex
	{
		get => this.maxTransparentAddressIndex;
		set => this.SetPropertyIfChanged(ref this.maxTransparentAddressIndex, value);
	}

	private string DebuggerDisplay => $"{this.DefaultAddress}";

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashAccount"/> class
	/// from the encoded form of one or more keys.
	/// </summary>
	/// <param name="encodedKey">The standard encoding of some key.</param>
	/// <param name="account">Receives the initialized account, if parsing is successful.</param>
	/// <returns><see langword="true" /> if <paramref name="encodedKey"/> was recognized as an encoding of some Zcash-related key that we support importing from; <see langword="false" /> otherwise.</returns>
	public static bool TryImportAccount(string encodedKey, [NotNullWhen(true)] out ZcashAccount? account)
	{
		Requires.NotNull(encodedKey);

		if (ZcashUtilities.TryParseKey(encodedKey, out IKeyWithTextEncoding? key))
		{
			return TryImportAccount(key, out account);
		}

		account = null;
		return false;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashAccount"/> class
	/// from some key.
	/// </summary>
	/// <param name="key">The key from which to create an account.</param>
	/// <param name="account">Receives the initialized account, if parsing is successful.</param>
	/// <returns><see langword="true" /> if <paramref name="key"/> was recognized as a key from which we can create an account; <see langword="false" /> otherwise.</returns>
	public static bool TryImportAccount(IKey key, [NotNullWhen(true)] out ZcashAccount? account)
	{
		Requires.NotNull(key);

		if (key is UnifiedViewingKey unifiedViewingKey)
		{
			account = new(unifiedViewingKey);
		}
		else if (key is ISpendingKey && key is Zip32HDWallet.IExtendedKey extendedSK)
		{
			account = new(SpendingKeys.FromKeys(extendedSK));
		}
		else if (key is IFullViewingKey fvk && FullViewingKeys.TryFromKeys([fvk], out FullViewingKeys? combinedFvk, out _))
		{
			account = new(combinedFvk);
		}
		else if (key is IIncomingViewingKey ivk && IncomingViewingKeys.TryFromKeys([ivk], out IncomingViewingKeys? combinedIvk, out _))
		{
			account = new(combinedIvk);
		}
		else
		{
			account = null;
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
	/// Gets the diversifier index for a given address, if the address came from this account.
	/// </summary>
	/// <param name="address">The address.</param>
	/// <param name="index">Receives the diversifier index used to construct this address.</param>
	/// <returns><see langword="true" /> if the address came from this account and the diversifier could be decrypted; otherwise <see langword="false" />.</returns>
	/// <remarks>
	/// <para>
	/// If the <paramref name="address"/> contains multiple diversifiable receivers,
	/// the diversifier index will only be reported from this method if it matches for all such receivers.
	/// </para>
	/// <para>
	/// Because there is no constant time path from transparent receiver to its address index,
	/// <em>transparent receivers are ignored</em> by this method.
	/// Callers that are interested in the address index of the <see cref="TransparentP2PKHReceiver"/>
	/// component of an address should use <see cref="TryGetTransparentIndex"/>
	/// directly to search for a match.
	/// </para>
	/// </remarks>
	public bool TryGetDiversifierIndex(ZcashAddress address, [NotNullWhen(true)] out DiversifierIndex? index)
	{
		Requires.NotNull(address, nameof(address));

		DiversifierIndex? orchardIndex = null;
		if (address.GetPoolReceiver<OrchardReceiver>() is { } orchard && this.IncomingViewing.Orchard is not null)
		{
			this.IncomingViewing.Orchard.TryGetDiversifierIndex(orchard, out orchardIndex);
		}

		DiversifierIndex? saplingIndex = null;
		if (address.GetPoolReceiver<SaplingReceiver>() is { } sapling && this.IncomingViewing.Sapling is not null)
		{
			this.IncomingViewing.Sapling.TryGetDiversifierIndex(sapling, out saplingIndex);
		}

		if (orchardIndex is not null && saplingIndex is not null)
		{
			// If both are present, they must match.
			if (!orchardIndex.Value.Equals(saplingIndex.Value))
			{
				index = null;
				return false;
			}

			index = orchardIndex;
			return true;
		}

		index = orchardIndex.HasValue ? orchardIndex.Value : saplingIndex;
		return index is not null;
	}

	/// <summary>
	/// Gets the address index for a given <see cref="TransparentAddress"/>, if the address came from this account
	/// and is within <see cref="Bip44MultiAccountHD.RecommendedAddressGapLimit"/> of <see cref="MaxTransparentAddressIndex"/>.
	/// </summary>
	/// <param name="address">The address whose index is sought.</param>
	/// <param name="change">Receives a value indicating which derivation path was used for the matching address if one was found; otherwise <see langword="null" />.</param>
	/// <param name="index">Receives the address index if a match was found; otherwise <see langword="null" />.</param>
	/// <returns><see langword="true" /> if a match could be found; otherwise <see langword="false" />.</returns>
	public bool TryGetTransparentIndex(TransparentAddress address, [NotNullWhen(true)] out Bip44MultiAccountHD.Change? change, [NotNullWhen(true)] out uint? index)
	{
		Requires.NotNull(address);

		if (this.IncomingViewing.Transparent is not null && this.MaxTransparentAddressIndex.HasValue)
		{
			uint maxIndex = this.MaxTransparentAddressIndex.Value + Bip44MultiAccountHD.RecommendedAddressGapLimit;
			if (address.GetPoolReceiver<TransparentP2PKHReceiver>() is { } p2pkh)
			{
				return this.IncomingViewing.Transparent.TryGetAddressIndex(p2pkh, maxIndex, out change, out index);
			}
			else if (address.GetPoolReceiver<TransparentP2SHReceiver>() is { } p2sh)
			{
				return this.IncomingViewing.Transparent.TryGetAddressIndex(p2sh, maxIndex, out change, out index);
			}
		}

		change = null;
		index = null;
		return false;
	}

	/// <summary>
	/// Gets a transparent address that sends ZEC to this account.
	/// </summary>
	/// <param name="index">
	/// The index of the address to produce. Avoid reusing an index for more than one sender to improve privacy.
	/// Using a value that is one greater than <see cref="MaxTransparentAddressIndex"/> is a good rule.
	/// </param>
	/// <returns>A transparent address.</returns>
	/// <remarks>
	/// If <paramref name="index"/> is greater than <see cref="MaxTransparentAddressIndex"/>, then <see cref="MaxTransparentAddressIndex"/> is updated to <paramref name="index"/>.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Throw when no transparent key is associated with this account.
	/// This can be tested for in advance by testing that the <see cref="IncomingViewingKeys.Transparent"/> property of the <see cref="IncomingViewing"/> property is not <see langword="null" />.
	/// </exception>
	public TransparentAddress GetTransparentAddress(uint index = 0)
	{
		Verify.Operation(this.IncomingViewing.Transparent is not null, "This account doesn't include any transparent keys.");

		this.MaxTransparentAddressIndex = this.MaxTransparentAddressIndex is null ? index : Math.Max(index, this.MaxTransparentAddressIndex.Value);
		return this.IncomingViewing.Transparent.GetReceivingKey(index).DefaultAddress;
	}

	/// <summary>
	/// Checks whether a given address sends ZEC to this account.
	/// </summary>
	/// <param name="address">The address to test.</param>
	/// <returns><see langword="true" /> if all receivers in <paramref name="address"/> are confirmed to direct ZEC to this account; <see langword="false" /> otherwise.</returns>
	/// <remarks>
	/// <para>
	/// There is a risk that a unified address containing multiple receivers may have been
	/// contrived to include receivers from this account and other receivers <em>not</em> belonging to this account.
	/// To avoid being tricked into reusing such a contrived address and unwittingly diverting ZEC to someone else's wallet,
	/// <see langword="false"/> is returned if any receiver does not belong to this account.
	/// </para>
	/// <para>
	/// This method will match on public and internal addresses for the account.
	/// Use the <see cref="AddressSendsToThisAccount(ZcashAddress, out bool?)"/> overload to determine whether the address is internal.
	/// </para>
	/// </remarks>
	public bool AddressSendsToThisAccount(ZcashAddress address) => this.AddressSendsToThisAccount(address, out _);

	/// <summary>
	/// Checks whether a given address sends ZEC to this account.
	/// </summary>
	/// <param name="address">The address to test.</param>
	/// <param name="isInternalAddress">
	/// Receives a value indicating whether the given <paramref name="address"/> is an <em>internal</em> address for this account.
	/// </param>
	/// <returns><see langword="true" /> if all receivers in <paramref name="address"/> are confirmed to direct ZEC to this account; <see langword="false" /> otherwise.</returns>
	/// <remarks>
	/// There is a risk that a unified address containing multiple receivers may have been
	/// contrived to include receivers from this account and other receivers <em>not</em> belonging to this account.
	/// To avoid being tricked into reusing such a contrived address and unwittingly diverting ZEC to someone else's wallet,
	/// <see langword="false"/> is returned if any receiver does not belong to this account.
	/// </remarks>
	public bool AddressSendsToThisAccount(ZcashAddress address, [NotNullWhen(true)] out bool? isInternalAddress)
	{
		Requires.NotNull(address);

		if (address is UnifiedAddress ua)
		{
			if (ua.Receivers.Count == 0)
			{
				isInternalAddress = null;
				return false;
			}

			isInternalAddress = null;
			foreach (ZcashAddress individualAddress in ua.Receivers)
			{
				if (!TestAddress(individualAddress, out isInternalAddress))
				{
					isInternalAddress = null;
					return false;
				}
			}

			// We don't expect an internal address to ever have multiple receivers,
			// since it is only used internally so a multi-receiver address has no purpose.
			// We also expect a matching address to have at least one receiver.
			// So isInternalAddress should be set exactly once at this point.
			// A contrived UA could theoretically have an internal receiver *and* an external receiver, but we'll ignore that edge case.
			Assumes.NotNull(isInternalAddress);

			return true;
		}
		else
		{
			return TestAddress(address, out isInternalAddress);
		}

		bool TestAddress(ZcashAddress individualAddress, [NotNullWhen(true)] out bool? isInternalAddress)
		{
			if (this.IncomingViewing.Orchard is not null && individualAddress.GetPoolReceiver<OrchardReceiver>() is { } orchardReceiver)
			{
				if (this.IncomingViewing.Orchard.CheckReceiver(orchardReceiver))
				{
					isInternalAddress = false;
					return true;
				}
				else if (this.FullViewing?.Internal.Orchard?.IncomingViewingKey.CheckReceiver(orchardReceiver) is true)
				{
					isInternalAddress = true;
					return true;
				}
			}

			if (this.IncomingViewing.Sapling is not null && individualAddress.GetPoolReceiver<SaplingReceiver>() is { } saplingReceiver)
			{
				if (this.IncomingViewing.Sapling.CheckReceiver(saplingReceiver))
				{
					isInternalAddress = false;
					return true;
				}
				else if (this.FullViewing?.Internal.Sapling?.IncomingViewingKey.CheckReceiver(saplingReceiver) is true)
				{
					isInternalAddress = true;
					return true;
				}
			}

			Zip32HDWallet.Transparent.ExtendedViewingKey? transparentViewing = this.FullViewing?.Transparent ?? this.IncomingViewing.Transparent;
			if (transparentViewing is not null)
			{
				if (individualAddress.GetPoolReceiver<TransparentP2PKHReceiver>() is { } p2pkhReceiver && transparentViewing.CheckReceiver(p2pkhReceiver, this.transparentAddressesToScanAsync))
				{
					isInternalAddress = false;
					return true;
				}

				if (individualAddress.GetPoolReceiver<TransparentP2SHReceiver>() is { } p2shReceiver && transparentViewing.CheckReceiver(p2shReceiver, this.transparentAddressesToScanAsync))
				{
					isInternalAddress = false;
					return true;
				}
			}

			isInternalAddress = null;
			return false;
		}
	}

	/// <summary>
	/// Raises the <see cref="PropertyChanged"/> event.
	/// </summary>
	/// <param name="propertyName">The name of the changed property.</param>
	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	/// <summary>
	/// Gets a diversifier index that is unique to this moment in time,
	/// for use as an input to the <see cref="GetDiversifiedAddress(ref DiversifierIndex)"/> method.
	/// </summary>
	/// <returns>The diversifier index.</returns>
	private static DiversifierIndex GetTimeBasedDiversifier() => new(DateTime.UtcNow.Ticks);

	/// <summary>
	/// Updates the value of a given field and raises the <see cref="PropertyChanged"/> event if a given value is different from the current value.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="field">The field to update.</param>
	/// <param name="value">The new value.</param>
	/// <param name="propertyName">The name of the property being set.</param>
	private void SetPropertyIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(field, value))
		{
			field = value;
			this.OnPropertyChanged(propertyName);
		}
	}

	/// <summary>
	/// Describes the parameters that were used to create this account.
	/// </summary>
	public record struct HDDerivationSource
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="HDDerivationSource"/> struct.
		/// </summary>
		/// <param name="wallet">A ZIP-32 wallet.</param>
		/// <param name="accountIndex">An account index. Must be less than 2^31.</param>
		public HDDerivationSource(Zip32HDWallet wallet, uint accountIndex)
		{
			Requires.Range(accountIndex < (2 ^ 31), nameof(accountIndex));

			this.Wallet = wallet;
			this.AccountIndex = accountIndex;
		}

		/// <summary>
		/// Gets the ZIP-32 derivation source.
		/// </summary>
		public Zip32HDWallet Wallet { get; init; }

		/// <summary>
		/// Gets the account index.
		/// </summary>
		public uint AccountIndex { get; init; }
	}

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
			Zip32HDWallet.Transparent.ExtendedSpendingKey? transparent,
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
		/// Gets the spending key for the transparent pool (<c>m/44'/133'/account'</c>).
		/// </summary>
		public Zip32HDWallet.Transparent.ExtendedSpendingKey? Transparent { get; }

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

		/// <inheritdoc/>
		IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

		/// <summary>
		/// Gets the full viewing key.
		/// </summary>
		internal FullViewingKeys FullViewingKey { get; }

		/// <summary>
		/// Gets the unified spending key for this account.
		/// </summary>
		internal UnifiedSpendingKey UnifiedKey { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="SpendingKeys"/> class.
		/// </summary>
		/// <param name="spendingKeys">An array of keys that may be included in this instance.</param>
		/// <returns>The newly created object.</returns>
		/// <exception cref="NotSupportedException">Thrown if any of the elements of <paramref name="spendingKeys"/> is not supported.</exception>
		internal static SpendingKeys FromKeys(params Zip32HDWallet.IExtendedKey[] spendingKeys)
		{
			Zip32HDWallet.Transparent.ExtendedSpendingKey? transparent = null;
			Zip32HDWallet.Sapling.ExtendedSpendingKey? sapling = null;
			Zip32HDWallet.Orchard.ExtendedSpendingKey? orchard = null;

			foreach (Zip32HDWallet.IExtendedKey key in spendingKeys)
			{
				if (key is Zip32HDWallet.Transparent.ExtendedSpendingKey t)
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
		internal FullViewingKeys(Zip32HDWallet.Transparent.ExtendedViewingKey? transparent, Sapling.DiversifiableFullViewingKey? sapling, Orchard.FullViewingKey? orchard)
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
		public Zip32HDWallet.Transparent.ExtendedViewingKey? Transparent { get; }

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

		/// <inheritdoc/>
		IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

		/// <summary>
		/// Gets the incoming viewing key.
		/// </summary>
		internal IncomingViewingKeys IncomingViewingKey { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="FullViewingKeys"/> class.
		/// </summary>
		/// <param name="fullViewingKeys">An array of keys that may be included in this instance.</param>
		/// <param name="result">Contains the resulting full viewing key object if successful.</param>
		/// <param name="unsupportedKeyIndex">The index into <paramref name="fullViewingKeys"/> of the first unsupported key, if unsuccessful.</param>
		/// <returns>A value indicating whether all the keys given in <paramref name="fullViewingKeys"/> are supported.</returns>
		internal static bool TryFromKeys(ReadOnlySpan<IFullViewingKey> fullViewingKeys, [NotNullWhen(true)] out FullViewingKeys? result, [NotNullWhen(false)] out int? unsupportedKeyIndex)
		{
			Zip32HDWallet.Transparent.ExtendedViewingKey? transparent = null;
			Sapling.DiversifiableFullViewingKey? sapling = null;
			Orchard.FullViewingKey? orchard = null;

			for (int i = 0; i < fullViewingKeys.Length; i++)
			{
				IFullViewingKey key = fullViewingKeys[i];
				if (key is Zip32HDWallet.Transparent.ExtendedViewingKey t)
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
					result = null;
					unsupportedKeyIndex = i;
					return false;
				}
			}

			result = new(transparent, sapling, orchard);
			unsupportedKeyIndex = null;
			return true;
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
		internal IncomingViewingKeys(Zip32HDWallet.Transparent.ExtendedViewingKey? transparent, Sapling.DiversifiableIncomingViewingKey? sapling, Orchard.IncomingViewingKey? orchard)
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
		public Zip32HDWallet.Transparent.ExtendedViewingKey? Transparent { get; }

		/// <summary>
		/// Gets the incoming viewing key for the sapling pool.
		/// </summary>
		public Sapling.DiversifiableIncomingViewingKey? Sapling { get; }

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
		/// <param name="result">Contains the resulting viewing key object if successful.</param>
		/// <param name="unsupportedKeyIndex">The index into <paramref name="incomingViewingKeys"/> of the first unsupported key, if unsuccessful.</param>
		/// <returns>A value indicating whether all the keys given in <paramref name="incomingViewingKeys"/> are supported.</returns>
		internal static bool TryFromKeys(ReadOnlySpan<IIncomingViewingKey> incomingViewingKeys, [NotNullWhen(true)] out IncomingViewingKeys? result, [NotNullWhen(false)] out int? unsupportedKeyIndex)
		{
			Zip32HDWallet.Transparent.ExtendedViewingKey? transparent = null;
			Sapling.DiversifiableIncomingViewingKey? sapling = null;
			Orchard.IncomingViewingKey? orchard = null;

			for (int i = 0; i < incomingViewingKeys.Length; i++)
			{
				IIncomingViewingKey key = incomingViewingKeys[i];
				if (key is Zip32HDWallet.Transparent.ExtendedViewingKey t)
				{
					transparent = t;
				}
				else if (key is Sapling.DiversifiableIncomingViewingKey s)
				{
					sapling = s;
				}
				else if (key is Orchard.IncomingViewingKey o)
				{
					orchard = o;
				}
				else
				{
					result = null;
					unsupportedKeyIndex = i;
					return false;
				}
			}

			result = new(transparent, sapling, orchard);
			unsupportedKeyIndex = null;
			return true;
		}
	}

	/// <summary>
	/// Equality comparers for <see cref="ZcashAccount"/>.
	/// </summary>
	public static class Equality
	{
		/// <summary>
		/// An equality comparer that considers two accounts to be equal if they have the same incoming viewing key.
		/// </summary>
		public static readonly IEqualityComparer<ZcashAccount> ByIncomingViewingKey = new EqualByIVK();

		/// <summary>
		/// An equality comparer that considers two accounts to be equal if they have the same full viewing key.
		/// </summary>
		/// <remarks>
		/// Accounts that have no full viewing key are not considered equal to any other account.
		/// </remarks>
		public static readonly IEqualityComparer<ZcashAccount> ByFullViewingKey = new EqualByFVK();

		/// <summary>
		/// An equality comparer that considers two accounts to be equal if they have the same spending key.
		/// </summary>
		/// <remarks>
		/// Accounts that have no spending key are not considered equal to any other account.
		/// </remarks>
		public static readonly IEqualityComparer<ZcashAccount> BySpendingKey = new EqualBySK();

		private class EqualBySK : IEqualityComparer<ZcashAccount>
		{
			public bool Equals(ZcashAccount? x, ZcashAccount? y)
			{
				if (ReferenceEquals(x, y))
				{
					return true;
				}

				if (x?.Spending is null || y?.Spending is null)
				{
					// If one or both are null, then they are not equal.
					// We do NOT consider two accounts without FVKs to be equal,
					// as that would be a security risk.
					return false;
				}

				return x.Spending.Equals(y.Spending);
			}

			public int GetHashCode([DisallowNull] ZcashAccount obj)
			{
				return obj.Spending?.GetHashCode() ?? obj.IncomingViewing.GetHashCode();
			}
		}

		private class EqualByFVK : IEqualityComparer<ZcashAccount>
		{
			public bool Equals(ZcashAccount? x, ZcashAccount? y)
			{
				if (ReferenceEquals(x, y))
				{
					return true;
				}

				if (x?.FullViewing is null || y?.FullViewing is null)
				{
					// If one or both are null, then they are not equal.
					// We do NOT consider two accounts without FVKs to be equal,
					// as that would be a security risk.
					return false;
				}

				return x.FullViewing.Equals(y.FullViewing);
			}

			public int GetHashCode([DisallowNull] ZcashAccount obj)
			{
				return obj.FullViewing?.GetHashCode() ?? obj.IncomingViewing.GetHashCode();
			}
		}

		private class EqualByIVK : IEqualityComparer<ZcashAccount>
		{
			public bool Equals(ZcashAccount? x, ZcashAccount? y)
			{
				if (ReferenceEquals(x, y))
				{
					return true;
				}

				if (x is null || y is null)
				{
					return false;
				}

				return x.IncomingViewing.Equals(y.IncomingViewing);
			}

			public int GetHashCode([DisallowNull] ZcashAccount obj)
			{
				return obj.IncomingViewing.GetHashCode();
			}
		}
	}
}
