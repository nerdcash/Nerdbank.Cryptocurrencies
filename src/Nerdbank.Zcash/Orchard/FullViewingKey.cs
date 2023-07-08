// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// A viewing key that can decrypt incoming and outgoing transactions.
/// </summary>
public class FullViewingKey : IKey
{
	private readonly Bytes96 rawEncoding;

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
	/// </summary>
	/// <param name="rawEncoding">The 96-byte raw encoding of the full viewing key.</param>
	/// <param name="isTestNet">A value indicating whether this key is for use on the testnet.</param>
	internal FullViewingKey(ReadOnlySpan<byte> rawEncoding, bool isTestNet)
	{
		this.rawEncoding = new(rawEncoding);
		this.IsTestNet = isTestNet;
	}

	/// <inheritdoc/>
	public bool IsTestNet { get; }

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

	/// <inheritdoc cref="CreateReceiver(ReadOnlySpan{byte})"/>
	public OrchardReceiver CreateReceiver(BigInteger diversifierIndex)
	{
		Span<byte> diversifierSpan = stackalloc byte[11];
		if (!diversifierIndex.TryWriteBytes(diversifierSpan, out _, isUnsigned: true))
		{
			throw new ArgumentOutOfRangeException(nameof(diversifierIndex), "Integer must be representable in 11 bytes.");
		}

		return this.CreateReceiver(diversifierSpan);
	}

	/// <summary>
	/// Creates an orchard receiver using this key and a given diversifier.
	/// </summary>
	/// <param name="diversifierIndex">An 11-byte deterministic diversifier.</param>
	/// <returns>The orchard receiver.</returns>
	public OrchardReceiver CreateReceiver(ReadOnlySpan<byte> diversifierIndex)
	{
		Span<byte> rawReceiver = stackalloc byte[43];
		if (NativeMethods.TryGetOrchardRawPaymentAddress(this.rawEncoding.Value, diversifierIndex, rawReceiver) != 0)
		{
			throw new InvalidKeyException(Strings.InvalidKey);
		}

		return new(rawReceiver);
	}

	/// <summary>
	/// Creates the default orchard receiver for this key.
	/// </summary>
	/// <returns>A receiver suitable for creating an address.</returns>
	/// <remarks>
	/// The default receiver is created using with a zero-filled diversifier.
	/// </remarks>
	public OrchardReceiver CreateDefaultReceiver()
	{
		Span<byte> diversifier = stackalloc byte[11];
		return this.CreateReceiver(diversifier);
	}
}
