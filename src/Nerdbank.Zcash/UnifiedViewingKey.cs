// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Nerdbank.Zcash;

/// <summary>
/// Represents the encoding of one or more viewing keys for a single logical account.
/// </summary>
/// <remarks>
/// This implements the Unified Viewing Keys part of the <see href="https://zips.z.cash/zip-0316">ZIP-316</see> specification.
/// </remarks>
public abstract class UnifiedViewingKey : IEnumerable<IIncomingViewingKey>, IIncomingViewingKey, IEquatable<UnifiedViewingKey>, IKeyWithTextEncoding
{
	private readonly IReadOnlyCollection<IUnifiedEncodingElement> viewingKeys;

	/// <summary>
	/// Initializes a new instance of the <see cref="UnifiedViewingKey"/> class.
	/// </summary>
	/// <param name="encoding">The string encoding of this viewing key.</param>
	/// <param name="viewingKeys">The viewing keys contained in this unified key.</param>
	/// <param name="network">The network this key operates on.</param>
	/// <param name="revision">The ZIP-316 revision number to which this particular unified viewing key adheres.</param>
	private protected UnifiedViewingKey(string encoding, IEnumerable<IIncomingViewingKey> viewingKeys, ZcashNetwork network, int revision)
	{
		this.TextEncoding = encoding;
		this.viewingKeys = viewingKeys.Cast<IUnifiedEncodingElement>().ToArray();
		this.Network = network;
		this.Revision = revision;
	}

	/// <summary>
	/// Gets the ZIP-316 revision number to which this particular unified viewing key adheres.
	/// </summary>
	public int Revision { get; }

	/// <summary>
	/// Gets the metadata associated with this unified viewing key.
	/// </summary>
	public UnifiedEncodingMetadata Metadata { get; protected init; } = UnifiedEncodingMetadata.Default;

	/// <summary>
	/// Gets the string encoding of the unified viewing key.
	/// </summary>
	public string TextEncoding { get; }

	/// <inheritdoc cref="IIncomingViewingKey.DefaultAddress"/>
	/// <remarks>
	/// Implemented as described <see href="https://zips.z.cash/zip-0316#deriving-a-unified-address-from-a-uivk">in ZIP-316</see>.
	/// Specifically, the address will have matching indexes across all receivers.
	/// </remarks>
	public UnifiedAddress DefaultAddress
	{
		get
		{
			DiversifierIndex index = default;
			Assumes.True(UnifiedAddress.TryCreate(ref index, this.viewingKeys.Cast<IIncomingViewingKey>(), this.Metadata, this.Revision, out UnifiedAddress? address));
			return address;
		}
	}

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
	public static implicit operator string?(UnifiedViewingKey? viewingKey) => viewingKey?.TextEncoding;

	/// <summary>
	/// Parses a unified viewing key into its component viewing keys.
	/// </summary>
	/// <param name="unifiedViewingKey">The string encoding of the unified viewing key.</param>
	/// <returns>The parsed unified viewing keys.</returns>
	/// <exception cref="InvalidKeyException">Thrown if any of the viewing keys fail to deserialize.</exception>
	public static UnifiedViewingKey Decode(string unifiedViewingKey)
	{
		Requires.NotNull(unifiedViewingKey);
		return TryDecode(unifiedViewingKey, out _, out string? errorMessage, out UnifiedViewingKey? result)
			? result
			: throw new InvalidKeyException(errorMessage);
	}

	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	static bool IKeyWithTextEncoding.TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
	{
		if (TryDecode(encoding, out decodeError, out errorMessage, out UnifiedViewingKey? uvk))
		{
			key = uvk;
			return true;
		}

		key = null;
		return false;
	}

	/// <summary>
	/// Parses a unified viewing key into its component viewing keys.
	/// </summary>
	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	public static bool TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out UnifiedViewingKey? key)
	{
		Requires.NotNull(encoding);
		if (!UnifiedEncoding.TryDecode(encoding, out string? humanReadablePart, out IReadOnlyList<UnifiedEncoding.UnknownElement>? elements, out decodeError, out errorMessage))
		{
			key = null;
			return false;
		}

		bool isFullViewingKey;
		ZcashNetwork network;
		int revision;
		switch (humanReadablePart)
		{
			case HumanReadablePart.R0.FVK.MainNet:
				isFullViewingKey = true;
				network = ZcashNetwork.MainNet;
				revision = 0;
				break;
			case HumanReadablePart.R0.IVK.MainNet:
				isFullViewingKey = false;
				network = ZcashNetwork.MainNet;
				revision = 0;
				break;
			case HumanReadablePart.R0.FVK.TestNet:
				isFullViewingKey = true;
				network = ZcashNetwork.TestNet;
				revision = 0;
				break;
			case HumanReadablePart.R0.IVK.TestNet:
				isFullViewingKey = false;
				network = ZcashNetwork.TestNet;
				revision = 0;
				break;
			case HumanReadablePart.R1.FVK.MainNet:
				isFullViewingKey = true;
				network = ZcashNetwork.MainNet;
				revision = 1;
				break;
			case HumanReadablePart.R1.IVK.MainNet:
				isFullViewingKey = false;
				network = ZcashNetwork.MainNet;
				revision = 1;
				break;
			case HumanReadablePart.R1.FVK.TestNet:
				isFullViewingKey = true;
				network = ZcashNetwork.TestNet;
				revision = 1;
				break;
			case HumanReadablePart.R1.IVK.TestNet:
				isFullViewingKey = false;
				network = ZcashNetwork.TestNet;
				revision = 1;
				break;
			default:
				decodeError = DecodeError.UnrecognizedHRP;
				errorMessage = Strings.UnrecognizedAddress;
				key = null;
				return false;
		}

		// Walk over each viewing key and metadata.
		List<IUnifiedEncodingElement> viewingKeys = new(elements.Count);
		UnifiedEncodingMetadata metadata = UnifiedEncodingMetadata.Default;
		foreach (UnifiedEncoding.UnknownElement element in elements)
		{
			switch (element.UnifiedTypeCode)
			{
				case UnifiedTypeCodes.Sapling:
					viewingKeys.Add(isFullViewingKey
						? Sapling.DiversifiableFullViewingKey.DecodeUnifiedViewingKeyContribution(element.Content.Span, network)
						: Sapling.DiversifiableIncomingViewingKey.DecodeUnifiedViewingKeyContribution(element.Content.Span, network));
					break;
				case UnifiedTypeCodes.Orchard:
					viewingKeys.Add(isFullViewingKey
						? Orchard.FullViewingKey.DecodeUnifiedViewingKeyContribution(element.Content.Span, network)
						: Orchard.IncomingViewingKey.DecodeUnifiedViewingKeyContribution(element.Content.Span, network));
					break;
				case UnifiedTypeCodes.TransparentP2PKH:
					viewingKeys.Add(Zip32HDWallet.Transparent.ExtendedViewingKey.DecodeUnifiedViewingKeyContribution(element.Content.Span, network, isFullViewingKey));
					break;
				case >= UnifiedTypeCodes.MustUnderstandTypeCodeStart and <= UnifiedTypeCodes.MustUnderstandTypeCodeEnd when revision == 0:
					decodeError = DecodeError.MustUnderstandMetadataNotAllowed;
					errorMessage = Strings.FormatMustUnderstandMetadataNotAllowedThisRevision(element.UnifiedTypeCode.ToString("x2"), revision);
					key = null;
					return false;
				case UnifiedTypeCodes.ExpirationByUnixTimeTypeCode:
					metadata = metadata with { ExpirationDate = UnifiedEncodingMetadata.DecodeExpirationDate(element.Content.Span) };
					break;
				case UnifiedTypeCodes.ExpirationByBlockHeightTypeCode:
					metadata = metadata with { ExpirationHeight = UnifiedEncodingMetadata.DecodeExpirationHeight(element.Content.Span) };
					break;
				case >= UnifiedTypeCodes.MustUnderstandTypeCodeStart and <= UnifiedTypeCodes.MustUnderstandTypeCodeEnd:
					decodeError = DecodeError.UnrecognizedMustUnderstandMetadata;
					errorMessage = Strings.FormatUnrecognizedMustUnderstandMetadata(element.UnifiedTypeCode.ToString("x2"));
					key = null;
					return false;
			}
		}

		key = isFullViewingKey
			? new Full(encoding, viewingKeys.Cast<IFullViewingKey>(), network, revision) { Metadata = metadata }
			: new Incoming(encoding, viewingKeys.Cast<IIncomingViewingKey>(), network, revision) { Metadata = metadata };
		decodeError = null;
		errorMessage = null;
		return true;
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

	/// <inheritdoc cref="TextEncoding"/>
	public override string ToString() => this.TextEncoding;

	/// <inheritdoc/>
	public bool Equals(UnifiedViewingKey? other)
	{
		if (other is null)
		{
			return false;
		}

		if (this.GetType() != other.GetType())
		{
			return false;
		}

		if (this.viewingKeys.Count != other.viewingKeys.Count)
		{
			return false;
		}

		foreach (IUnifiedEncodingElement element in this.viewingKeys)
		{
			IUnifiedEncodingElement? otherElement = other.viewingKeys.FirstOrDefault(vk => element.UnifiedTypeCode == vk.UnifiedTypeCode);
			if (otherElement is null)
			{
				return false;
			}

			// Because unified encoding can be lossy, allow for these elements to compare themselves
			// with this lossiness in mind, if they wish.
			if (element is IUnifiedEncodingElementEqualityComparer comparer)
			{
				if (!comparer.Equals(otherElement as IUnifiedEncodingElementEqualityComparer))
				{
					return false;
				}
			}
			else if (!element.Equals(otherElement))
			{
				return false;
			}
		}

		if (this.Metadata != other.Metadata)
		{
			return false;
		}

		return true;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => this.Equals(obj as UnifiedViewingKey);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode result = default;

		result.Add(this.viewingKeys.Count);
		foreach (IUnifiedEncodingElement element in this.viewingKeys)
		{
			result.Add(element is IUnifiedEncodingElementEqualityComparer comparer ? comparer.GetHashCode() : element.GetHashCode());
		}

		return result.ToHashCode();
	}

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
	/// <param name="metadata">The metadata to encode into this key.</param>
	/// <param name="minimumRevision">The minimum revision to encode.</param>
	/// <returns>The encoded unified viewing key, and the network it operates on.</returns>
	/// <exception cref="NotSupportedException">Thrown if any of the viewing keys do not implement <see cref="IUnifiedEncodingElement"/>.</exception>
	private protected static (string Encoding, ZcashNetwork Network, int Revision) Prepare(IEnumerable<IIncomingViewingKey> viewingKeys, bool isFullViewingKey, UnifiedEncodingMetadata metadata, int minimumRevision)
	{
		ZcashNetwork? network = null;
		bool hasShieldedElement = false;
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

			hasShieldedElement |= key.DefaultAddress.HasShieldedReceiver;
		}

		Requires.Argument(network is not null, nameof(viewingKeys), "Cannot create a unified viewing key with no viewing keys.");
		int revision = Math.Max(minimumRevision, metadata.HasMustUnderstandMetadata || !hasShieldedElement ? 1 : 0);

		string humanReadablePart = (network, isFullViewingKey, revision) switch
		{
			(ZcashNetwork.MainNet, true, 0) => HumanReadablePart.R0.FVK.MainNet,
			(ZcashNetwork.TestNet, true, 0) => HumanReadablePart.R0.FVK.TestNet,
			(ZcashNetwork.MainNet, false, 0) => HumanReadablePart.R0.IVK.MainNet,
			(ZcashNetwork.TestNet, false, 0) => HumanReadablePart.R0.IVK.TestNet,
			(ZcashNetwork.MainNet, true, 1) => HumanReadablePart.R1.FVK.MainNet,
			(ZcashNetwork.TestNet, true, 1) => HumanReadablePart.R1.FVK.TestNet,
			(ZcashNetwork.MainNet, false, 1) => HumanReadablePart.R1.IVK.MainNet,
			(ZcashNetwork.TestNet, false, 1) => HumanReadablePart.R1.IVK.TestNet,
			_ => throw new NotSupportedException("Unrecognized Zcash network."),
		};

		string unifiedEncoding = UnifiedEncoding.Encode(humanReadablePart, viewingKeys.Cast<IUnifiedEncodingElement>(), metadata);

		return (unifiedEncoding, network.Value, revision);
	}

	/// <summary>
	/// A unified incoming viewing key.
	/// </summary>
	public class Incoming : UnifiedViewingKey
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Incoming"/> class.
		/// </summary>
		/// <inheritdoc cref="UnifiedViewingKey(string, IEnumerable{IIncomingViewingKey}, ZcashNetwork, int)"/>
		internal Incoming(string encoding, IEnumerable<IIncomingViewingKey> viewingKeys, ZcashNetwork network, int revision)
			: base(encoding, viewingKeys, network, revision)
		{
		}

		/// <inheritdoc cref="Create(IEnumerable{IIncomingViewingKey})"/>
		public static Incoming Create(params IIncomingViewingKey[] viewingKeys) => Create((IEnumerable<IIncomingViewingKey>)viewingKeys);

		/// <inheritdoc cref="Create(IEnumerable{IIncomingViewingKey}, UnifiedEncodingMetadata)"/>
		public static Incoming Create(IEnumerable<IIncomingViewingKey> viewingKeys) => Create(viewingKeys, UnifiedEncodingMetadata.Default);

		/// <inheritdoc cref="Create(IEnumerable{IIncomingViewingKey}, UnifiedEncodingMetadata, int)"/>
		public static Incoming Create(IEnumerable<IIncomingViewingKey> viewingKeys, UnifiedEncodingMetadata metadata) => Create(viewingKeys, metadata, 0);

		/// <summary>
		/// Constructs a unified viewing key from a set of viewing keys.
		/// </summary>
		/// <param name="viewingKeys">
		/// The viewing keys to include in the unified viewing key.
		/// This must not be empty.
		/// This must not include more than one viewing key for a given pool.
		/// The set of keys must be of a consistent viewing type (e.g. all full viewing keys or all incoming viewing keys).
		/// </param>
		/// <param name="metadata">Metadata to apply to the key.</param>
		/// <param name="minimumRevision">The minimum revision to use for the encoding.</param>
		/// <returns>The unified viewing key.</returns>
		internal static Incoming Create(IEnumerable<IIncomingViewingKey> viewingKeys, UnifiedEncodingMetadata metadata, int minimumRevision)
		{
			Requires.NotNull(viewingKeys);
			Requires.NotNull(metadata);

			IEnumerable<IIncomingViewingKey> ivks = viewingKeys
				.Select(vk => vk.ReduceToOnlyIVK());

			(string encoding, ZcashNetwork network, int revision) = Prepare(ivks, isFullViewingKey: false, metadata, minimumRevision);
			return new(encoding, ivks, network, revision) { Metadata = metadata };
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
		/// <inheritdoc cref="UnifiedViewingKey(string, IEnumerable{IIncomingViewingKey}, ZcashNetwork, int)"/>
		internal Full(string encoding, IEnumerable<IFullViewingKey> viewingKeys, ZcashNetwork network, int revision)
			: base(encoding, viewingKeys, network, revision)
		{
		}

		/// <summary>
		/// Gets the unified incoming viewing key.
		/// </summary>
		/// <remarks>
		/// Implemented as described <see href="https://zips.z.cash/zip-0316#deriving-a-uivk-from-a-ufvk">in ZIP-316</see>.
		/// </remarks>
		public Incoming IncomingViewingKey => Incoming.Create(this.viewingKeys.Cast<IIncomingViewingKey>(), this.Metadata, this.Revision);

		/// <inheritdoc/>
		IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

		/// <inheritdoc cref="Create(IEnumerable{IFullViewingKey})"/>
		public static Full Create(params IFullViewingKey[] viewingKeys) => Create((IEnumerable<IFullViewingKey>)viewingKeys);

		/// <inheritdoc cref="Create(IEnumerable{IFullViewingKey}, UnifiedEncodingMetadata)"/>
		public static Full Create(IEnumerable<IFullViewingKey> viewingKeys) => Create(viewingKeys, UnifiedEncodingMetadata.Default);

		/// <summary>
		/// Constructs a unified viewing key from a set of viewing keys.
		/// </summary>
		/// <param name="viewingKeys">
		/// The viewing keys to include in the unified viewing key.
		/// This must not be empty.
		/// This must not include more than one viewing key for a given pool.
		/// </param>
		/// <param name="metadata">The metadata to encode into the key.</param>
		/// <returns>The unified viewing key.</returns>
		public static Full Create(IEnumerable<IFullViewingKey> viewingKeys, UnifiedEncodingMetadata metadata)
		{
			Requires.NotNull(viewingKeys);
			Requires.NotNull(metadata);

			IEnumerable<IFullViewingKey> fvks = viewingKeys
				.Select(vk => vk.ReduceToOnlyFVK());

			(string encoding, ZcashNetwork network, int revision) = Prepare(fvks, isFullViewingKey: true, metadata, minimumRevision: 0);
			return new(encoding, fvks, network, revision) { Metadata = metadata };
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

	/// <summary>
	/// The human readable parts for Unified Viewing Keys.
	/// </summary>
	private protected static class HumanReadablePart
	{
		private const string TestNetSuffix = "test";

		/// <summary>
		/// For UVK revision 0.
		/// </summary>
		internal static class R0
		{
			private const string Prefix = "u";

			/// <summary>
			/// HRPs for full viewing keys.
			/// </summary>
			internal static class FVK
			{
				/// <summary>
				/// The human-readable part of a Unified Full Viewing Key on mainnet, revision 0.
				/// </summary>
				internal const string MainNet = $"{Prefix}view";

				/// <summary>
				/// The human-readable part of a Unified Full Viewing Key on testnet, revision 0.
				/// </summary>
				internal const string TestNet = $"{MainNet}{TestNetSuffix}";
			}

			/// <summary>
			/// HRPs for incoming viewing keys.
			/// </summary>
			internal static class IVK
			{
				/// <summary>
				/// The human-readable part of a Unified Incoming Viewing Key on mainnet, revision 0.
				/// </summary>
				internal const string MainNet = $"{Prefix}ivk";

				/// <summary>
				/// The human-readable part of a Unified Incoming Viewing Key on testnet, revision 0.
				/// </summary>
				internal const string TestNet = $"{MainNet}{TestNetSuffix}";
			}
		}

		/// <summary>
		/// For UVK revision 1.
		/// </summary>
		internal static class R1
		{
			private const string Prefix = "ur";

			/// <summary>
			/// HRPs for full viewing keys.
			/// </summary>
			internal static class FVK
			{
				/// <summary>
				/// The human-readable part of a Unified Full Viewing Key on mainnet, revision 1.
				/// </summary>
				internal const string MainNet = $"{Prefix}view";

				/// <summary>
				/// The human-readable part of a Unified Full Viewing Key on testnet, revision 1.
				/// </summary>
				internal const string TestNet = $"{MainNet}{TestNetSuffix}";
			}

			/// <summary>
			/// HRPs for incoming viewing keys.
			/// </summary>
			internal static class IVK
			{
				/// <summary>
				/// The human-readable part of a Unified Incoming Viewing Key on mainnet, revision 1.
				/// </summary>
				internal const string MainNet = $"{Prefix}ivk";

				/// <summary>
				/// The human-readable part of a Unified Incoming Viewing Key on testnet, revision 1.
				/// </summary>
				internal const string TestNet = $"{MainNet}{TestNetSuffix}";
			}
		}
	}
}
