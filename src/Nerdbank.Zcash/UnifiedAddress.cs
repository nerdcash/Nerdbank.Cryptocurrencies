// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Nerdbank.Zcash;

/// <summary>
/// A <see href="https://zips.z.cash/zip-0316">unified Zcash address</see>.
/// </summary>
public abstract class UnifiedAddress : ZcashAddress
{
	/// <summary>
	/// The human-readable part of a Unified Address on mainnet.
	/// </summary>
	private protected const string HumanReadablePartMainNet = "u";

	/// <summary>
	/// The human-readable part of a Unified Address on testnet.
	/// </summary>
	private protected const string HumanReadablePartTestNet = "utest";

	/// <summary>
	/// Initializes a new instance of the <see cref="UnifiedAddress"/> class.
	/// </summary>
	/// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
	public UnifiedAddress(string address)
		: base(address)
	{
	}

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

	/// <summary>
	/// Creates a unified address from a list of receiver addresses.
	/// </summary>
	/// <param name="receivers">
	/// The receivers to build into the unified address.
	/// These will be sorted by preferred order before being encoded into the address.
	/// No more than one of each type of address is allowed.
	/// Sprout addresses are not allowed.
	/// </param>
	/// <returns>A unified address that contains all the receivers.</returns>
	public static UnifiedAddress Create(IReadOnlyCollection<ZcashAddress> receivers)
	{
		Requires.NotNull(receivers);
		Requires.Argument(receivers.Count > 0, nameof(receivers), "Cannot create a unified address with no receivers.");

		if (receivers.Count == 1 && receivers.Single() is UnifiedAddress existingUnifiedAddress)
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

		Requires.Argument(hasShieldedAddress, nameof(receivers), "At least one shielded address is required.");

		string humanReadablePart = network switch
		{
			ZcashNetwork.MainNet => HumanReadablePartMainNet,
			ZcashNetwork.TestNet => HumanReadablePartTestNet,
			_ => throw new NotSupportedException(),
		};

		string unifiedChars = UnifiedEncoding.Encode(humanReadablePart, receivers.Cast<IUnifiedEncodingElement>());

		return new CompoundUnifiedAddress(unifiedChars, new(GetReceiversInPreferredOrder(receivers)));
	}

	/// <inheritdoc cref="ZcashAddress.TryDecode(string, out DecodeError?, out string?, out ZcashAddress?)" />
	internal static bool TryParse(string address, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out UnifiedAddress? result)
	{
		if (!UnifiedEncoding.TryDecode(address, out string? humanReadablePart, out IReadOnlyList<UnifiedEncoding.UnknownElement>? unknownElements, out errorCode, out errorMessage))
		{
			result = null;
			return false;
		}

		ZcashNetwork network;
		switch (humanReadablePart)
		{
			case HumanReadablePartMainNet:
				network = ZcashNetwork.MainNet;
				break;
			case HumanReadablePartTestNet:
				network = ZcashNetwork.TestNet;
				break;
			default:
				errorCode = DecodeError.UnrecognizedHRP;
				errorMessage = Strings.UnrecognizedAddress;
				result = null;
				return false;
		}

		// Walk over each receiver.
		List<ZcashAddress> receiverAddresses = new(unknownElements.Count);
		foreach (UnifiedEncoding.UnknownElement element in unknownElements)
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
			}
		}

		// If we parsed exactly one Orchard receiver, just return it as its own address.
		errorCode = null;
		errorMessage = null;
		result = receiverAddresses.Count == 1 && receiverAddresses[0] is OrchardAddress orchardAddr
			? orchardAddr
			: new CompoundUnifiedAddress(address, new(GetReceiversInPreferredOrder(receiverAddresses)));
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
}
