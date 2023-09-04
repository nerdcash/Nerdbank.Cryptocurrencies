// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// A viewing key that can decrypt incoming and outgoing transactions.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class FullViewingKey : IUnifiedEncodingElement, IFullViewingKey, IEquatable<FullViewingKey>
{
	private readonly Bytes96 rawEncoding;

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
	/// </summary>
	/// <param name="rawEncoding">The 96-byte raw encoding of the full viewing key.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal FullViewingKey(ReadOnlySpan<byte> rawEncoding, ZcashNetwork network)
	{
		this.rawEncoding = new(rawEncoding);
		this.IncomingViewingKey = IncomingViewingKey.FromFullViewingKey(rawEncoding, network);
	}

	/// <summary>
	/// Gets the network this key should be used with.
	/// </summary>
	public ZcashNetwork Network => this.IncomingViewingKey.Network;

	/// <summary>
	/// Gets the incoming viewing key.
	/// </summary>
	public IncomingViewingKey IncomingViewingKey { get; }

	/// <inheritdoc/>
	IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

	/// <inheritdoc/>
	byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.Orchard;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.UnifiedDataLength => 32 * 3;

	/// <summary>
	/// Gets the spend validating key.
	/// </summary>
	internal SpendValidatingKey Ak => new(this.rawEncoding.Value[0..32]);

	/// <summary>
	/// Gets the nullifier deriving key.
	/// </summary>
	internal NullifierDerivingKey Nk => new(this.rawEncoding.Value[32..64]);

	/// <summary>
	/// Gets the commit randomness.
	/// </summary>
	internal CommitIvkRandomness Rivk => new(this.rawEncoding.Value[64..]);

	/// <summary>
	/// Gets the 96-byte raw encoding of this key.
	/// </summary>
	internal ReadOnlySpan<byte> RawEncoding => this.rawEncoding.Value;

	private string DebuggerDisplay => this.IncomingViewingKey.DefaultAddress.ToString();

	/// <inheritdoc/>
	public bool Equals(FullViewingKey? other)
	{
		return other is not null
			&& this.rawEncoding.Value.SequenceEqual(other.rawEncoding.Value)
			&& this.Network == other.Network;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is FullViewingKey other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode result = default;
		result.Add(this.Network);
		result.AddBytes(this.rawEncoding.Value);
		return result.ToHashCode();
	}

	/// <inheritdoc/>
	int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination)
	{
		int written = 0;
		written += this.rawEncoding.Value.CopyToRetLength(destination[written..]);
		return written;
	}

	/// <summary>
	/// Reads the viewing key from its representation in a unified viewing key.
	/// </summary>
	/// <param name="keyContribution">The data that would have been written by <see cref="IUnifiedEncodingElement.WriteUnifiedData(Span{byte})"/>.</param>
	/// <param name="network">The network the key should be used with.</param>
	/// <returns>The deserialized key.</returns>
	internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network)
	{
		return new FullViewingKey(keyContribution, network);
	}
}
