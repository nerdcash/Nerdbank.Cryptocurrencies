// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Nerdbank.Zcash;

/// <summary>
/// Represents the encoding of one or more viewing keys for a single logical account.
/// </summary>
public abstract class UnifiedViewingKey : IEnumerable<IIncomingViewingKey>, IIncomingViewingKey
{
	private const string HumanReadablePartMainNetFVK = "uview";
	private const string HumanReadablePartTestNetFVK = "uviewtest";
	private const string HumanReadablePartMainNetIVK = "uivk";
	private const string HumanReadablePartTestNetIVK = "uivktest";

	private readonly IReadOnlyCollection<IUnifiedEncodingElement> viewingKeys;

	/// <summary>
	/// Initializes a new instance of the <see cref="UnifiedViewingKey"/> class.
	/// </summary>
	/// <param name="encoding">The string encoding of this viewing key.</param>
	/// <param name="viewingKeys">The viewing keys contained in this unified key.</param>
	/// <param name="network">The network this key operates on.</param>
	private protected UnifiedViewingKey(string encoding, IEnumerable<IIncomingViewingKey> viewingKeys, ZcashNetwork network)
	{
		this.ViewingKey = encoding;
		this.viewingKeys = viewingKeys.Cast<IUnifiedEncodingElement>().ToArray();
		this.Network = network;
	}

	/// <summary>
	/// Gets the string encoding of the unified viewing key.
	/// </summary>
	public string ViewingKey { get; }

	/// <inheritdoc cref="IIncomingViewingKey.DefaultAddress"/>
	/// <remarks>
	/// Implemented as described <see href="https://zips.z.cash/zip-0316#deriving-a-unified-address-from-a-uivk">in ZIP-316</see>.
	/// </remarks>
	public UnifiedAddress DefaultAddress => UnifiedAddress.Create(this.viewingKeys.Select(vk => ((IIncomingViewingKey)vk).DefaultAddress).ToArray());

	/// <inheritdoc/>
	ZcashAddress IIncomingViewingKey.DefaultAddress => this.DefaultAddress;

	/// <summary>
	/// Gets the network that the keys in this unified viewing key should be used on.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Implicitly casts this viewing key to its string encoding.
	/// </summary>
	/// <param name="viewingKey">The viewing key to convert.</param>
	[return: NotNullIfNotNull(nameof(viewingKey))]
	public static implicit operator string?(UnifiedViewingKey? viewingKey) => viewingKey?.ViewingKey;

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
		where T : class, IIncomingViewingKey
	{
		return this.viewingKeys.OfType<T>().FirstOrDefault();
	}

	/// <inheritdoc cref="ViewingKey"/>
	public override string ToString() => this.ViewingKey;

	/// <inheritdoc/>
	public IEnumerator<IIncomingViewingKey> GetEnumerator() => this.viewingKeys.Cast<IIncomingViewingKey>().GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	/// <summary>
	/// Encodes the specified viewing keys.
	/// </summary>
	/// <param name="viewingKeys">The viewing keys to encode.</param>
	/// <param name="isFullViewingKey">
	/// <see langword="true" /> to produce a full viewing key; <see langword="false" /> to produce an incoming viewing key.
	/// The caller is responsible to ensure that all <paramref name="viewingKeys"/> are all consistently
	/// the same kind (full or incoming).
	/// </param>
	/// <returns>The encoded unified viewing key, and the network it operates on.</returns>
	/// <exception cref="NotSupportedException">Thrown if any of the viewing keys do not implement <see cref="IUnifiedEncodingElement"/>.</exception>
	private protected static (string Encoding, ZcashNetwork Network) Prepare(IEnumerable<IIncomingViewingKey> viewingKeys, bool isFullViewingKey)
	{
		ZcashNetwork? network = null;
		foreach (IIncomingViewingKey key in viewingKeys)
		{
			if (network is null)
			{
				network = key.Network;
			}
			else
			{
				Requires.Argument(network == key.Network, nameof(viewingKeys), "All viewing keys must belong to the same network.");
			}

			if (key is not IUnifiedEncodingElement)
			{
				throw new NotSupportedException($"Key {key.GetType()} is not supported in a unified viewing key.");
			}
		}

		Requires.Argument(network is not null, nameof(viewingKeys), "Cannot create a unified viewing key with no viewing keys.");

		string humanReadablePart = (network, isFullViewingKey) switch
		{
			(ZcashNetwork.MainNet, true) => HumanReadablePartMainNetFVK,
			(ZcashNetwork.TestNet, true) => HumanReadablePartTestNetFVK,
			(ZcashNetwork.MainNet, false) => HumanReadablePartMainNetIVK,
			(ZcashNetwork.TestNet, false) => HumanReadablePartTestNetIVK,
			_ => throw new NotSupportedException("Unrecognized Zcash network."),
		};

		string unifiedEncoding = UnifiedEncoding.Encode(humanReadablePart, viewingKeys.Cast<IUnifiedEncodingElement>());

		return (unifiedEncoding, network.Value);
	}

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

		result = isFullViewingKey
			? new Full(unifiedViewingKey, viewingKeys.Cast<IFullViewingKey>(), network)
			: new Incoming(unifiedViewingKey, viewingKeys.Cast<IIncomingViewingKey>(), network);
		errorCode = null;
		errorMessage = null;
		return true;
	}

	/// <summary>
	/// A unified incoming viewing key.
	/// </summary>
	public class Incoming : UnifiedViewingKey
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Incoming"/> class.
		/// </summary>
		/// <inheritdoc cref="UnifiedViewingKey(string, IEnumerable{IIncomingViewingKey}, ZcashNetwork)"/>
		internal Incoming(string encoding, IEnumerable<IIncomingViewingKey> viewingKeys, ZcashNetwork network)
			: base(encoding, viewingKeys, network)
		{
		}

		/// <inheritdoc cref="Create(IEnumerable{IIncomingViewingKey})"/>
		public static Incoming Create(params IIncomingViewingKey[] viewingKeys) => Create((IEnumerable<IIncomingViewingKey>)viewingKeys);

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
		public static Incoming Create(IEnumerable<IIncomingViewingKey> viewingKeys)
		{
			Requires.NotNull(viewingKeys);

			IEnumerable<IIncomingViewingKey> ivks = viewingKeys
				.Select(vk => vk.ReduceToOnlyIVK());

			(string encoding, ZcashNetwork network) = Prepare(ivks, isFullViewingKey: false);
			return new(encoding, ivks, network);
		}
	}

	/// <summary>
	/// A unified full viewing key.
	/// </summary>
	public class Full : UnifiedViewingKey, IEnumerable<IFullViewingKey>, IFullViewingKey
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Full"/> class.
		/// </summary>
		/// <inheritdoc cref="UnifiedViewingKey(string, IEnumerable{IIncomingViewingKey}, ZcashNetwork)"/>
		internal Full(string encoding, IEnumerable<IFullViewingKey> viewingKeys, ZcashNetwork network)
			: base(encoding, viewingKeys, network)
		{
		}

		/// <summary>
		/// Gets the unified incoming viewing key.
		/// </summary>
		/// <remarks>
		/// Implemented as described <see href="https://zips.z.cash/zip-0316#deriving-a-uivk-from-a-ufvk">in ZIP-316</see>.
		/// </remarks>
		public Incoming IncomingViewingKey => Incoming.Create(this.viewingKeys.Cast<IIncomingViewingKey>());

		/// <inheritdoc/>
		IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

		/// <inheritdoc cref="Create(IEnumerable{IFullViewingKey})"/>
		public static Full Create(params IFullViewingKey[] viewingKeys) => Create((IEnumerable<IFullViewingKey>)viewingKeys);

		/// <summary>
		/// Constructs a unified viewing key from a set of viewing keys.
		/// </summary>
		/// <param name="viewingKeys">
		/// The viewing keys to include in the unified viewing key.
		/// This must not be empty.
		/// This must not include more than one viewing key for a given pool.
		/// </param>
		/// <returns>The unified viewing key.</returns>
		public static Full Create(IEnumerable<IFullViewingKey> viewingKeys)
		{
			Requires.NotNull(viewingKeys);

			IEnumerable<IFullViewingKey> fvks = viewingKeys
				.Select(vk => vk.ReduceToOnlyFVK());

			(string encoding, ZcashNetwork network) = Prepare(fvks, isFullViewingKey: true);
			return new(encoding, fvks, network);
		}

		/// <summary>
		/// Gets the viewing key of a given type, if included in this key.
		/// </summary>
		/// <typeparam name="T">The type of the viewing key.</typeparam>
		/// <returns>The viewing key, if found; otherwise <see langword="null" />.</returns>
		public new T? GetViewingKey<T>()
			where T : class, IFullViewingKey
		{
			return this.viewingKeys.OfType<T>().FirstOrDefault();
		}

		/// <inheritdoc/>
		public new IEnumerator<IFullViewingKey> GetEnumerator() => this.viewingKeys.Cast<IFullViewingKey>().GetEnumerator();

		/// <inheritdoc/>
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	}
}
