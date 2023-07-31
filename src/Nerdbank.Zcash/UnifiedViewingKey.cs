// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Security.Cryptography;

namespace Nerdbank.Zcash;

/// <summary>
/// Represents the encoding of one or more viewing keys for a single logical account.
/// </summary>
public class UnifiedViewingKey : IEnumerable<IViewingKey>
{
	private const string HumanReadablePartMainNetFVK = "uview";
	private const string HumanReadablePartTestNetFVK = "uviewtest";
	private const string HumanReadablePartMainNetIVK = "uivk";
	private const string HumanReadablePartTestNetIVK = "uivktest";

	private readonly IReadOnlyCollection<IUnifiedEncodableViewingKey> viewingKeys;

	private UnifiedViewingKey(string viewingKey, bool isFullViewingKey, ZcashNetwork network, IReadOnlyCollection<IUnifiedEncodableViewingKey> viewingKeys)
	{
		this.ViewingKey = viewingKey;
		this.viewingKeys = viewingKeys;
		this.IsFullViewingKey = isFullViewingKey;
		this.Network = network;
	}

	/// <summary>
	/// Gets the string encoding of the unified viewing key.
	/// </summary>
	public string ViewingKey { get; }

	/// <summary>
	/// Gets the network that the keys in this unified viewing key should be used on.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Gets a value indicating whether this viewing key includes the ability to view outgoing transactions.
	/// </summary>
	/// <remarks>
	/// When <see langword="false" />, only incoming transactions may be viewed.
	/// </remarks>
	public bool IsFullViewingKey { get; }

	/// <summary>
	/// Implicitly casts this viewing key to its string encoding.
	/// </summary>
	/// <param name="viewingKey">The viewing key to convert.</param>
	[return: NotNullIfNotNull(nameof(viewingKey))]
	public static implicit operator string?(UnifiedViewingKey? viewingKey) => viewingKey?.ViewingKey;

	/// <inheritdoc cref="Create(IReadOnlyCollection{IViewingKey})"/>
	public static UnifiedViewingKey Create(params IViewingKey[] viewingKeys) => Create((IReadOnlyCollection<IViewingKey>)viewingKeys);

	/// <summary>
	/// Constructs a unified viewing key from a set of viewing keys.
	/// </summary>
	/// <param name="viewingKeys">
	/// The viewing keys to include in the unified viewing key.
	/// This must not be empty.
	/// This must not include more than one viewing key for a given pool.
	/// The set of keys must be of a consistent viewing type (e.g. all full viewing keys or all incoming viewing keys).
	/// </param>
	/// <returns>The unified viewing key.</returns>
	public static UnifiedViewingKey Create(IReadOnlyCollection<IViewingKey> viewingKeys)
	{
		Requires.NotNull(viewingKeys);
		Requires.Argument(viewingKeys.Count > 0, nameof(viewingKeys), "Cannot create a unified viewing key with no viewing keys.");

		SortedDictionary<byte, IUnifiedEncodableViewingKey> sortedKeysByTypeCode = new();
		int totalLength = UnifiedEncoding.PaddingLength;
		IViewingKey firstKey = viewingKeys.First();

		ZcashNetwork network = firstKey.Network;
		bool isFullViewingKey = firstKey.IsFullViewingKey;

		foreach (IViewingKey key in viewingKeys)
		{
			Requires.Argument(network == key.Network, nameof(viewingKeys), "All viewing keys must belong to the same network.");
			Requires.Argument(isFullViewingKey == key.IsFullViewingKey, nameof(viewingKeys), "All viewing keys must be full or all must be incoming viewing keys. A mix of these types is not supported.");

			if (key is IUnifiedEncodableViewingKey unifiableKey)
			{
				byte typeCode = unifiableKey.UnifiedTypeCode;
				Requires.Argument(!sortedKeysByTypeCode.ContainsKey(typeCode), nameof(viewingKeys), $"Only one viewing key per pool is allowed, but two with typecode {typeCode} were included.");
				sortedKeysByTypeCode.Add(typeCode, unifiableKey);

				totalLength += 1; // type code
				totalLength += CompactSize.GetEncodedLength((ulong)unifiableKey.UnifiedKeyContributionLength);
				totalLength += unifiableKey.UnifiedKeyContributionLength;
			}
			else
			{
				throw new NotSupportedException($"Key {key.GetType()} is not supported in a unified viewing key.");
			}
		}

		string humanReadablePart = (network, isFullViewingKey) switch
		{
			(ZcashNetwork.MainNet, true) => HumanReadablePartMainNetFVK,
			(ZcashNetwork.TestNet, true) => HumanReadablePartTestNetFVK,
			(ZcashNetwork.MainNet, false) => HumanReadablePartMainNetIVK,
			(ZcashNetwork.TestNet, false) => HumanReadablePartTestNetIVK,
			_ => throw new NotSupportedException("Unrecognized Zcash network."),
		};

		Span<byte> uvk = stackalloc byte[totalLength];
		int uvkBytesWritten = 0;
		foreach (KeyValuePair<byte, IUnifiedEncodableViewingKey> typeCodeAndKey in sortedKeysByTypeCode)
		{
			uvk[uvkBytesWritten++] = typeCodeAndKey.Key;
			uvkBytesWritten += CompactSize.Encode((ulong)typeCodeAndKey.Value.UnifiedKeyContributionLength, uvk[uvkBytesWritten..]);
			uvkBytesWritten += typeCodeAndKey.Value.WriteUnifiedViewingKeyContribution(uvk[uvkBytesWritten..]);
		}

		uvkBytesWritten += UnifiedEncoding.InitializePadding(humanReadablePart, uvk.Slice(uvkBytesWritten, UnifiedEncoding.PaddingLength));

		Assumes.True(uvkBytesWritten == uvk.Length);

		UnifiedEncoding.F4Jumble(uvk);

		Span<char> result = stackalloc char[Bech32.GetEncodedLength(humanReadablePart.Length, uvk.Length)];
		int finalLength = Bech32.Bech32m.Encode(humanReadablePart, uvk, result);
		Assumes.True(result.Length == finalLength);

		return new UnifiedViewingKey(result.ToString(), isFullViewingKey, network, viewingKeys.Cast<IUnifiedEncodableViewingKey>().ToArray());
	}

	/// <summary>
	/// Parses a unified viewing key into its component viewing keys.
	/// </summary>
	/// <param name="unifiedViewingKey">The string encoding of the unified viewing key.</param>
	/// <returns>The parsed unified viewing keys.</returns>
	/// <exception cref="InvalidKeyException">Thrown if any of the viewing keys fail to deserialize.</exception>
	public static UnifiedViewingKey Parse(string unifiedViewingKey)
	{
		return TryParse(unifiedViewingKey, out UnifiedViewingKey? result, out ParseError? errorCode, out string? errorMessage)
			? result
			: throw new InvalidKeyException(errorMessage);
	}

	/// <summary>
	/// Parses a unified viewing key into its component viewing keys.
	/// </summary>
	/// <param name="unifiedViewingKey">The string encoding of the unified viewing key.</param>
	/// <param name="result">Receives the parsed viewing key, if successful.</param>
	/// <returns>A value indicating whether parsing was successful.</returns>
	public static bool TryParse(string unifiedViewingKey, [NotNullWhen(true)] out UnifiedViewingKey? result) => TryParse(unifiedViewingKey, out result, out _, out _);

	/// <summary>
	/// Gets the viewing key of a given type, if included in this key.
	/// </summary>
	/// <typeparam name="T">The type of the viewing key.</typeparam>
	/// <returns>The viewing key, if found; otherwise <see langword="null" />.</returns>
	public T? GetViewingKey<T>()
		where T : IViewingKey
	{
		return this.viewingKeys.OfType<T>().FirstOrDefault();
	}

	/// <inheritdoc/>
	public override string ToString() => this.ViewingKey;

	/// <inheritdoc/>
	public IEnumerator<IViewingKey> GetEnumerator() => this.viewingKeys.GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	private static bool TryParse(string unifiedViewingKey, [NotNullWhen(true)] out UnifiedViewingKey? result, [NotNullWhen(false)] out ParseError? errorCode, [NotNullWhen(false)] out string? errorMessage)
	{
		Requires.NotNull(unifiedViewingKey);

		(int Tag, int Data)? length = Bech32.GetDecodedLength(unifiedViewingKey);
		if (length is null)
		{
			errorCode = ParseError.UnrecognizedAddressType;
			errorMessage = Strings.UnrecognizedAddress;
			result = null;
			return false;
		}

		Span<char> humanReadablePart = stackalloc char[length.Value.Tag];
		Span<byte> data = stackalloc byte[length.Value.Data];
		if (!Bech32.Bech32m.TryDecode(unifiedViewingKey, humanReadablePart, data, out DecodeError? decodeError, out errorMessage, out _))
		{
			errorCode = ZcashAddress.DecodeToParseError(decodeError);
			result = null;
			return false;
		}

		bool isFullViewingKey;
		ZcashNetwork network;
		if (humanReadablePart.SequenceEqual(HumanReadablePartMainNetFVK))
		{
			isFullViewingKey = true;
			network = ZcashNetwork.MainNet;
		}
		else if (humanReadablePart.SequenceEqual(HumanReadablePartMainNetIVK))
		{
			isFullViewingKey = false;
			network = ZcashNetwork.MainNet;
		}
		else if (humanReadablePart.SequenceEqual(HumanReadablePartTestNetFVK))
		{
			isFullViewingKey = true;
			network = ZcashNetwork.TestNet;
		}
		else if (humanReadablePart.SequenceEqual(HumanReadablePartTestNetIVK))
		{
			isFullViewingKey = false;
			network = ZcashNetwork.TestNet;
		}
		else
		{
			errorCode = ParseError.UnrecognizedAddressType;
			errorMessage = Strings.UnrecognizedAddress;
			result = null;
			return false;
		}

		if (length.Value.Data is < UnifiedEncoding.MinF4JumbleInputLength or > UnifiedEncoding.MaxF4JumbleInputLength)
		{
			errorCode = ParseError.InvalidAddress;
			errorMessage = Strings.InvalidAddressLength;
			result = null;
			return false;
		}

		UnifiedEncoding.F4Jumble(data, inverted: true);

		// Verify the 16-byte padding is as expected.
		Span<byte> padding = stackalloc byte[UnifiedEncoding.PaddingLength];
		UnifiedEncoding.InitializePadding(humanReadablePart, padding);
		if (!data[^padding.Length..].SequenceEqual(padding))
		{
			errorCode = ParseError.InvalidAddress;
			errorMessage = Strings.InvalidPadding;
			result = null;
			return false;
		}

		// Strip the padding.
		data = data[..^padding.Length];

		// Walk over each viewing key.
		List<IUnifiedEncodableViewingKey> viewingKeys = new();
		while (data.Length > 0)
		{
			byte typeCode = data[0];
			data = data[1..];
			data = data[CompactSize.Decode(data, out ulong keyLengthUL)..];
			int keyLength = checked((int)keyLengthUL);

			// Process each receiver type we support, and quietly ignore any we don't.
			if (data.Length < keyLength)
			{
				errorCode = ParseError.InvalidAddress;
				errorMessage = $"Expected data length {keyLength} but remaining data had only {data.Length} bytes left.";
				result = null;
				return false;
			}

			ReadOnlySpan<byte> viewingKeyData = data[..keyLength];
			IUnifiedEncodableViewingKey? viewingKey = typeCode switch
			{
				0x02 => isFullViewingKey
						? Sapling.DiversifiableFullViewingKey.DecodeUnifiedViewingKeyContribution(viewingKeyData, network)
						: Sapling.IncomingViewingKey.DecodeUnifiedViewingKeyContribution(viewingKeyData, network),
				0x03 => isFullViewingKey
						? Orchard.FullViewingKey.DecodeUnifiedViewingKeyContribution(viewingKeyData, network)
						: throw new NotImplementedException(),
				_ => null,
			};

			if (viewingKey is not null)
			{
				viewingKeys.Add(viewingKey);
			}

			// Move on to the next receiver.
			data = data[keyLength..];
		}

		result = new UnifiedViewingKey(unifiedViewingKey, isFullViewingKey, network, viewingKeys);
		errorCode = null;
		errorMessage = null;
		return true;
	}
}
