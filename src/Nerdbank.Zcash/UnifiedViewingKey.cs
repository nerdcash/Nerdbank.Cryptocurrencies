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

	private readonly IReadOnlyCollection<IUnifiedEncodingElement> viewingKeys;

	private UnifiedViewingKey(string viewingKey, bool isFullViewingKey, ZcashNetwork network, IReadOnlyCollection<IUnifiedEncodingElement> viewingKeys)
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

		IViewingKey firstKey = viewingKeys.First();
		ZcashNetwork network = firstKey.Network;
		bool isFullViewingKey = firstKey.IsFullViewingKey;

		foreach (IViewingKey key in viewingKeys)
		{
			Requires.Argument(network == key.Network, nameof(viewingKeys), "All viewing keys must belong to the same network.");
			Requires.Argument(isFullViewingKey == key.IsFullViewingKey, nameof(viewingKeys), "All viewing keys must be full or all must be incoming viewing keys. A mix of these types is not supported.");
			if (key is not IUnifiedEncodingElement)
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

		string unifiedEncoding = UnifiedEncoding.Encode(humanReadablePart, viewingKeys.Cast<IUnifiedEncodingElement>());

		return new UnifiedViewingKey(unifiedEncoding, isFullViewingKey, network, viewingKeys.Cast<IUnifiedEncodingElement>().ToArray());
	}

	/// <summary>
	/// Parses a unified viewing key into its component viewing keys.
	/// </summary>
	/// <param name="unifiedViewingKey">The string encoding of the unified viewing key.</param>
	/// <returns>The parsed unified viewing keys.</returns>
	/// <exception cref="InvalidKeyException">Thrown if any of the viewing keys fail to deserialize.</exception>
	public static UnifiedViewingKey Parse(string unifiedViewingKey)
	{
		Requires.NotNull(unifiedViewingKey);
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
	public static bool TryParse(string unifiedViewingKey, [NotNullWhen(true)] out UnifiedViewingKey? result)
	{
		Requires.NotNull(unifiedViewingKey);
		return TryParse(unifiedViewingKey, out result, out _, out _);
	}

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
	public IEnumerator<IViewingKey> GetEnumerator() => this.viewingKeys.Cast<IViewingKey>().GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	private static bool TryParse(string unifiedViewingKey, [NotNullWhen(true)] out UnifiedViewingKey? result, [NotNullWhen(false)] out ParseError? errorCode, [NotNullWhen(false)] out string? errorMessage)
	{
		if (!UnifiedEncoding.TryDecode(unifiedViewingKey, out string? humanReadablePart, out IReadOnlyList<UnifiedEncoding.UnknownElement>? unknownElements, out errorCode, out errorMessage))
		{
			result = null;
			return false;
		}

		bool isFullViewingKey;
		ZcashNetwork network;
		switch (humanReadablePart)
		{
			case HumanReadablePartMainNetFVK:
				isFullViewingKey = true;
				network = ZcashNetwork.MainNet;
				break;
			case HumanReadablePartMainNetIVK:
				isFullViewingKey = false;
				network = ZcashNetwork.MainNet;
				break;
			case HumanReadablePartTestNetFVK:
				isFullViewingKey = true;
				network = ZcashNetwork.TestNet;
				break;
			case HumanReadablePartTestNetIVK:
				isFullViewingKey = false;
				network = ZcashNetwork.TestNet;
				break;
			default:
				errorCode = ParseError.UnrecognizedAddressType;
				errorMessage = Strings.UnrecognizedAddress;
				result = null;
				return false;
		}

		// Walk over each viewing key.
		List<IUnifiedEncodingElement> viewingKeys = new(unknownElements.Count);
		foreach (UnifiedEncoding.UnknownElement element in unknownElements)
		{
			IUnifiedEncodingElement? viewingKey = element.UnifiedTypeCode switch
			{
				UnifiedTypeCodes.Sapling => isFullViewingKey
						? Sapling.DiversifiableFullViewingKey.DecodeUnifiedViewingKeyContribution(element.Content.Span, network)
						: Sapling.IncomingViewingKey.DecodeUnifiedViewingKeyContribution(element.Content.Span, network),
				UnifiedTypeCodes.Orchard => isFullViewingKey
						? Orchard.FullViewingKey.DecodeUnifiedViewingKeyContribution(element.Content.Span, network)
						: Orchard.IncomingViewingKey.DecodeUnifiedViewingKeyContribution(element.Content.Span, network),
				_ => element,
			};

			viewingKeys.Add(viewingKey);
		}

		result = new UnifiedViewingKey(unifiedViewingKey, isFullViewingKey, network, viewingKeys);
		errorCode = null;
		errorMessage = null;
		return true;
	}
}
