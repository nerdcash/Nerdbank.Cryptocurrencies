// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// The incoming viewing key for the Orchard pool.
/// </summary>
public class IncomingViewingKey : IUnifiedEncodingElement, IViewingKey, IEquatable<IncomingViewingKey>
{
	private readonly Bytes64 rawEncoding;

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class.
	/// </summary>
	/// <param name="rawEncoding">The raw encoding of the IVK (dk + ivk).</param>
	/// <param name="network">The network the key should be used on.</param>
	private IncomingViewingKey(ReadOnlySpan<byte> rawEncoding, ZcashNetwork network)
	{
		this.Network = network;
		this.rawEncoding = new(rawEncoding);
	}

	/// <inheritdoc/>
	public ZcashNetwork Network { get; }

	/// <inheritdoc/>
	bool IViewingKey.IsFullViewingKey => false;

	/// <inheritdoc/>
	bool IKey.IsTestNet => this.Network != ZcashNetwork.MainNet;

	/// <inheritdoc/>
	byte IUnifiedEncodingElement.UnifiedTypeCode => 0x03;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.UnifiedDataLength => this.Dk.Value.Length + this.Ivk.Value.Length;

	/// <summary>
	/// Gets the diversifier key.
	/// </summary>
	internal DiversifierKey Dk => new(this.rawEncoding.Value[..32]);

	/// <summary>
	/// Gets the key agreement private key.
	/// </summary>
	internal KeyAgreementPrivateKey Ivk => new(this.rawEncoding.Value[32..]);

	/// <summary>
	/// Gets the raw encoding of this incoming viewing key.
	/// </summary>
	internal ReadOnlySpan<byte> RawEncoding => this.rawEncoding.Value;

	/// <inheritdoc/>
	public bool Equals(IncomingViewingKey? other)
	{
		return other is not null
			&& this.Network == other.Network
			&& this.rawEncoding.Value.SequenceEqual(other.rawEncoding.Value);
	}

	/// <inheritdoc/>
	int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination) => this.Encode(destination);

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class
	/// from its raw encoding.
	/// </summary>
	/// <param name="buffer">The buffer to read from. 64-bytes will be read from this.</param>
	/// <param name="network">The network the key should be used on.</param>
	/// <returns>The incoming viewing key.</returns>
	internal static IncomingViewingKey Decode(ReadOnlySpan<byte> buffer, ZcashNetwork network) => new(buffer, network);

	/// <summary>
	/// Reads the viewing key from its representation in a unified viewing key.
	/// </summary>
	/// <param name="keyContribution">The data that would have been written by <see cref="IUnifiedEncodingElement.WriteUnifiedData(Span{byte})"/>.</param>
	/// <param name="network">The network the key should be used with.</param>
	/// <returns>The deserialized key.</returns>
	internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network) => Decode(keyContribution, network);

	/// <summary>
	/// Initializen a new instance of the <see cref="IncomingViewingKey" /> class
	/// from the raw encoding of a full viewing key.
	/// </summary>
	/// <param name="fvkRawEncoding">The raw encoding of the full viewing key.</param>
	/// <param name="network">The network the key should be used on.</param>
	/// <returns>The incoming viewing key.</returns>
	/// <exception cref="InvalidKeyException">Thrown when the full viewing key encoding is invalid.</exception>
	internal static IncomingViewingKey FromFullViewingKey(ReadOnlySpan<byte> fvkRawEncoding, ZcashNetwork network)
	{
		Span<byte> ivk = stackalloc byte[64];
		if (NativeMethods.GetOrchardIncomingViewingKeyFromFullViewingKey(fvkRawEncoding, ivk) != 0)
		{
			throw new InvalidKeyException();
		}

		return new IncomingViewingKey(ivk, network);
	}

	/// <summary>
	/// Writes out the raw encoding of the incoming viewing key.
	/// </summary>
	/// <param name="buffer">The buffer to write to. Must be at least 64 bytes.</param>
	/// <returns>The number of bytes written. Always 64.</returns>
	internal int Encode(Span<byte> buffer)
	{
		int written = 0;
		written += this.Dk.Value.CopyToRetLength(buffer[written..]);
		written += this.Ivk.Value.CopyToRetLength(buffer[written..]);
		return written;
	}
}
