// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Nerdbank.Zcash;

/// <summary>
/// A <see href="https://zips.z.cash/zip-0316">unified Zcash address</see>.
/// </summary>
/// <remarks>
/// <para>
/// Per <see href="https://zips.z.cash/zip-0316#requirements-for-both-unified-addresses-and-unified-viewing-keys">ZIP-316</see>,
/// any abbreviation of this address for UI purposes MUST include at least the first 20 characters.
/// </para>
/// </remarks>
public abstract class UnifiedAddress : ZcashAddress
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UnifiedAddress"/> class.
	/// </summary>
	/// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
	/// <param name="revision">The ZIP-316 revision number to which this particular unified address adheres.</param>
	protected UnifiedAddress(string address, int revision)
		: base(address)
	{
		this.Revision = revision;
	}

	/// <summary>
	/// Gets the ZIP-316 revision number to which this particular unified address adheres.
	/// </summary>
	public int Revision { get; }

	/// <summary>
	/// Gets the metadata associated with this address.
	/// </summary>
	public UnifiedEncodingMetadata Metadata { get; protected init; } = UnifiedEncodingMetadata.Default;

	/// <inheritdoc/>
	public override ZcashNetwork Network => this.Receivers[0].Network;

	/// <summary>
	/// Gets the receivers for this address, in order of preference.
	/// </summary>
	/// <remarks>
	/// <para>Every address has at least one receiver. An <see cref="OrchardAddress"/> will produce only itself.</para>
	/// </remarks>
	public abstract IReadOnlyList<ZcashAddress> Receivers { get; }

	/// <inheritdoc cref="Create(IReadOnlyCollection{ZcashAddress})"/>
	public static UnifiedAddress Create(params ZcashAddress[] receivers) => Create((IReadOnlyCollection<ZcashAddress>)receivers);

	/// <inheritdoc cref="TryCreate(ref DiversifierIndex, IEnumerable{IIncomingViewingKey}, UnifiedEncodingMetadata, out UnifiedAddress?)"/>
	public static bool TryCreate(ref DiversifierIndex startingDiversiferIndex, IEnumerable<IIncomingViewingKey> keys, [NotNullWhen(true)] out UnifiedAddress? address)
		=> TryCreate(ref startingDiversiferIndex, keys, UnifiedEncodingMetadata.Default, minimumRevision: 0, out address);

	/// <inheritdoc cref="TryCreate(ref DiversifierIndex, IEnumerable{IIncomingViewingKey}, UnifiedEncodingMetadata, int, out UnifiedAddress?)"/>
	public static bool TryCreate(ref DiversifierIndex startingDiversiferIndex, IEnumerable<IIncomingViewingKey> keys, UnifiedEncodingMetadata metadata, [NotNullWhen(true)] out UnifiedAddress? address)
		=> TryCreate(ref startingDiversiferIndex, keys, metadata, minimumRevision: 0, out address);

	/// <inheritdoc cref="Create(IReadOnlyCollection{ZcashAddress}, UnifiedEncodingMetadata)"/>
	public static UnifiedAddress Create(IReadOnlyCollection<ZcashAddress> receivers) => Create(receivers, UnifiedEncodingMetadata.Default);

	/// <inheritdoc cref="Create(IReadOnlyCollection{ZcashAddress}, UnifiedEncodingMetadata, int)"/>
	public static UnifiedAddress Create(IReadOnlyCollection<ZcashAddress> receivers, UnifiedEncodingMetadata metadata) => Create(receivers, metadata, 0);

	/// <summary>
	/// Gets a copy of this address with the specified metadata.
	/// </summary>
	/// <param name="metadata">The new set of metadata to apply to the address. This will replace all metadata previously in the address.</param>
	/// <returns>The address with <paramref name="metadata"/> applied.</returns>
	public UnifiedAddress WithMetadata(UnifiedEncodingMetadata metadata) => metadata == this.Metadata ? this : Create(this.Receivers, metadata);

	/// <summary>
	/// Creates a unified address from a list of address sources.
	/// </summary>
	/// <param name="startingDiversiferIndex">The diversifier index from which to start the search for an index that is compatible with all address sources.</param>
	/// <param name="keys">The keys to use to build the receivers for the unified address.</param>
	/// <param name="metadata">The metadata to include in the address.</param>
	/// <param name="minimumRevision">The desired revision for the resulting address.</param>
	/// <param name="address">Receives the unified address, if one could be created.</param>
	/// <returns>A value indicating whether the address was successfully created.</returns>
	/// <remarks>
	/// Per <see href="https://github.com/zcash/zips/blob/fee271c03da36836c58e649d9674fa430e343810/zip-0316.rst#L690-L692">ZIP-316</see>, a unified address
	/// should only be created with receivers that all agree on the diversifier index.
	/// This method automates construction of a compliant address.
	/// </remarks>
	internal static bool TryCreate(ref DiversifierIndex startingDiversiferIndex, IEnumerable<IIncomingViewingKey> keys, UnifiedEncodingMetadata metadata, int minimumRevision, [NotNullWhen(true)] out UnifiedAddress? address)
	{
		Requires.NotNull(keys);

		List<ZcashAddress> receivers = new();

		// Search for sapling first since it may need to adjust the diversifier.
		IIncomingViewingKey? saplingKey = null;
		foreach (IIncomingViewingKey key in keys)
		{
			IIncomingViewingKey ivk = (key as IFullViewingKey)?.IncomingViewingKey ?? key;

			if (ivk is Sapling.DiversifiableIncomingViewingKey sapling)
			{
				if (!sapling.TryCreateReceiver(ref startingDiversiferIndex, out SaplingReceiver? receiver))
				{
					// The diversifier index was so high that sapling had no more valid diversified addresses available.
					address = null;
					return false;
				}

				receivers.Add(new SaplingAddress(receiver.Value, sapling.Network));
				saplingKey = key;
				break;
			}
		}

		foreach (IIncomingViewingKey key in keys)
		{
			if (key == saplingKey)
			{
				// We already handled this one.
				continue;
			}

			IIncomingViewingKey ivk = (key as IFullViewingKey)?.IncomingViewingKey ?? key;

			if (ivk is Orchard.IncomingViewingKey orchard)
			{
				receivers.Add(new OrchardAddress(orchard.CreateReceiver(startingDiversiferIndex), orchard.Network));
			}
			else if (ivk is Zip32HDWallet.Transparent.ExtendedViewingKey transparent)
			{
				// Ensure that the diversifier isn't too high as it only has 31-bits available.
				BigInteger bigInt = startingDiversiferIndex.ToBigInteger();
				if (bigInt.GetBitLength() > 31)
				{
					address = null;
					return false;
				}

				receivers.Add(transparent.GetReceivingKey((uint)bigInt).DefaultAddress);
			}
			else
			{
				throw new NotSupportedException($"Unsupported key type found: {ivk.GetType()}");
			}
		}

		address = Create(receivers, metadata, minimumRevision);
		return true;
	}

	/// <summary>
	/// Creates a unified address from a list of receiver addresses.
	/// </summary>
	/// <param name="receivers">
	/// The receivers to build into the unified address.
	/// These will be sorted by preferred order before being encoded into the address.
	/// No more than one of each type of address is allowed.
	/// Sprout addresses are not allowed.
	/// </param>
	/// <param name="metadata">Metadata to embed into the address.</param>
	/// <param name="minimumRevision">The minimum revision to create.</param>
	/// <returns>A unified address that contains all the receivers.</returns>
	internal static UnifiedAddress Create(IReadOnlyCollection<ZcashAddress> receivers, UnifiedEncodingMetadata metadata, int minimumRevision)
	{
		Requires.NotNull(receivers);
		Requires.Argument(receivers.Count > 0, nameof(receivers), "Cannot create a unified address with no receivers.");
		Requires.NotNull(metadata);

		if (receivers.Count == 1 && receivers.Single() is UnifiedAddress existingUnifiedAddress && existingUnifiedAddress.Metadata == metadata)
		{
			// If the only receiver is a UA, just return it.
			return existingUnifiedAddress;
		}

		bool hasShieldedAddress = false;
		bool hasTransparentAddress = false;
		ZcashNetwork? network = null;
		foreach (ZcashAddress receiver in receivers)
		{
			try
			{
				hasShieldedAddress |= receiver.UnifiedTypeCode > UnifiedTypeCodes.TransparentP2SH;
			}
			catch (NotSupportedException ex)
			{
				throw new ArgumentException("Unified Addresses with multiple receivers cannot be specified as one receiver in another UA.", ex);
			}

			if (receiver is TransparentAddress)
			{
				if (hasTransparentAddress)
				{
					throw new ArgumentException("Unified Addresses may carry at most one transparent address.");
				}

				hasTransparentAddress = true;
			}

			if (network is null)
			{
				network = receiver.Network;
			}
			else if (network != receiver.Network)
			{
				throw new ArgumentException(Strings.MixingNetworksInUANotAllowed, nameof(receivers));
			}
		}

		int revision = Math.Max(minimumRevision, metadata.HasMustUnderstandMetadata || !hasShieldedAddress ? 1 : 0);

		string humanReadablePart = (network, revision) switch
		{
			(ZcashNetwork.MainNet, 0) => HumanReadablePart.R0.MainNet,
			(ZcashNetwork.TestNet, 0) => HumanReadablePart.R0.TestNet,
			(ZcashNetwork.MainNet, 1) => HumanReadablePart.R1.MainNet,
			(ZcashNetwork.TestNet, 1) => HumanReadablePart.R1.TestNet,
			_ => throw new NotSupportedException(),
		};

		string unifiedChars = UnifiedEncoding.Encode(humanReadablePart, receivers.Cast<IUnifiedEncodingElement>(), metadata);

		return new CompoundUnifiedAddress(unifiedChars, new(GetReceiversInPreferredOrder(receivers)), revision)
		{
			Metadata = metadata,
		};
	}

	/// <inheritdoc cref="ZcashAddress.TryDecode(string, out DecodeError?, out string?, out ZcashAddress?)" />
	internal static bool TryParse(string address, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out UnifiedAddress? result)
	{
		if (!UnifiedEncoding.TryDecode(address, out string? humanReadablePart, out IReadOnlyList<UnifiedEncoding.UnknownElement>? elements, out errorCode, out errorMessage))
		{
			result = null;
			return false;
		}

		ZcashNetwork network;
		int revision;
		switch (humanReadablePart)
		{
			case HumanReadablePart.R0.MainNet:
				network = ZcashNetwork.MainNet;
				revision = 0;
				break;
			case HumanReadablePart.R0.TestNet:
				network = ZcashNetwork.TestNet;
				revision = 0;
				break;
			case HumanReadablePart.R1.MainNet:
				network = ZcashNetwork.MainNet;
				revision = 1;
				break;
			case HumanReadablePart.R1.TestNet:
				network = ZcashNetwork.TestNet;
				revision = 1;
				break;
			default:
				errorCode = DecodeError.UnrecognizedHRP;
				errorMessage = Strings.UnrecognizedAddress;
				result = null;
				return false;
		}

		// Walk over each receiver.
		List<ZcashAddress> receiverAddresses = new(elements.Count);
		UnifiedEncodingMetadata metadata = UnifiedEncodingMetadata.Default;
		foreach (UnifiedEncoding.UnknownElement element in elements)
		{
			switch (element.UnifiedTypeCode)
			{
				case UnifiedTypeCodes.TransparentP2PKH:
					receiverAddresses.Add(new TransparentP2PKHAddress(new TransparentP2PKHReceiver(element.Content.Span), network));
					break;
				case UnifiedTypeCodes.TransparentP2SH:
					receiverAddresses.Add(new TransparentP2SHAddress(new TransparentP2SHReceiver(element.Content.Span), network));
					break;
				case UnifiedTypeCodes.Sapling:
					receiverAddresses.Add(new SaplingAddress(new SaplingReceiver(element.Content.Span), network));
					break;
				case UnifiedTypeCodes.Orchard:
					receiverAddresses.Add(new OrchardAddress(new OrchardReceiver(element.Content.Span), network));
					break;
				case >= UnifiedTypeCodes.MustUnderstandTypeCodeStart and <= UnifiedTypeCodes.MustUnderstandTypeCodeEnd when revision == 0:
					errorCode = DecodeError.MustUnderstandMetadataNotAllowed;
					errorMessage = Strings.FormatMustUnderstandMetadataNotAllowedThisRevision(element.UnifiedTypeCode.ToString("x2"), revision);
					result = null;
					return false;
				case UnifiedTypeCodes.ExpirationByUnixTimeTypeCode:
					metadata = metadata with { ExpirationDate = UnifiedEncodingMetadata.DecodeExpirationDate(element.Content.Span) };
					break;
				case UnifiedTypeCodes.ExpirationByBlockHeightTypeCode:
					metadata = metadata with { ExpirationHeight = UnifiedEncodingMetadata.DecodeExpirationHeight(element.Content.Span) };
					break;
				case >= UnifiedTypeCodes.MustUnderstandTypeCodeStart and <= UnifiedTypeCodes.MustUnderstandTypeCodeEnd:
					errorCode = DecodeError.UnrecognizedMustUnderstandMetadata;
					errorMessage = Strings.FormatUnrecognizedMustUnderstandMetadata(element.UnifiedTypeCode.ToString("x2"));
					result = null;
					return false;
			}
		}

		// If we parsed exactly one Orchard receiver, just return it as its own address.
		errorCode = null;
		errorMessage = null;
		result = receiverAddresses.Count == 1 && receiverAddresses[0] is OrchardAddress orchardAddr && orchardAddr.Metadata == metadata
			? orchardAddr.WithMetadata(metadata)
			: new CompoundUnifiedAddress(address, new(GetReceiversInPreferredOrder(receiverAddresses)), revision) { Metadata = metadata };
		return true;
	}

	private static ReadOnlyCollection<ZcashAddress> GetReceiversInPreferredOrder(IReadOnlyCollection<ZcashAddress> addresses)
	{
		// Although the UA encoding requires the receivers to be sorted in ascending Type Code order,
		// we want to list receivers in order of preference, which is the opposite.
		List<ZcashAddress> sortedAddresses = addresses.ToList();
		sortedAddresses.Sort((a, b) => -a.UnifiedTypeCode.CompareTo(b.UnifiedTypeCode));
		return new(sortedAddresses);
	}

	/// <summary>
	/// The human readable parts for Unified Addresses.
	/// </summary>
	private protected static class HumanReadablePart
	{
		/// <summary>
		/// For UA revision 0.
		/// </summary>
		internal static class R0
		{
			/// <summary>
			/// The human-readable part of a Unified Address on mainnet, revision 0.
			/// </summary>
			internal const string MainNet = "u";

			/// <summary>
			/// The human-readable part of a Unified Address on testnet, revision 0.
			/// </summary>
			internal const string TestNet = $"{MainNet}test";
		}

		/// <summary>
		/// For UA revision 1.
		/// </summary>
		internal static class R1
		{
			/// <summary>
			/// The human-readable part of a Unified Address on mainnet, revision 1.
			/// </summary>
			internal const string MainNet = "ur";

			/// <summary>
			/// The human-readable part of a Unified Address on testnet, revision 1.
			/// </summary>
			internal const string TestNet = $"{MainNet}test";
		}
	}
}
