// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// A viewing key that can decrypt incoming and outgoing transactions.
/// </summary>
[DebuggerDisplay($"{{{nameof(DefaultAddress)},nq}}")]
public class FullViewingKey : IUnifiedEncodingElement, IViewingKey, IEquatable<FullViewingKey>
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
		this.Network = network;
	}

	/// <summary>
	/// Gets the network this key should be used with.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <inheritdoc/>
	bool IViewingKey.IsFullViewingKey => true;

	/// <inheritdoc/>
	byte IUnifiedEncodingElement.UnifiedTypeCode => 0x03;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.UnifiedDataLength => 32 * 3;

	/// <inheritdoc/>
	bool IKey.IsTestNet => this.Network != ZcashNetwork.MainNet;

	/// <summary>
	/// Gets the default address for this spending key.
	/// </summary>
	/// <remarks>
	/// Create additional diversified addresses using <see cref="CreateReceiver(BigInteger)"/>.
	/// </remarks>
	/// <seealso cref="CreateDefaultReceiver"/>
	/// <seealso cref="CreateReceiver(BigInteger)"/>
	public OrchardAddress DefaultAddress => new(this.CreateDefaultReceiver(), this.Network);

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

	/// <summary>
	/// Checks whether a given orchard receiver was derived from the same spending authority as this key
	/// (in other words: would ZEC sent to this receiver arrive in this account?).
	/// </summary>
	/// <param name="receiver">The receiver to test.</param>
	/// <returns><see langword="true"/> if this receiver would send ZEC to this account; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// <para>This is a simpler front-end for the <see cref="TryGetDiversifierIndex(OrchardReceiver, Span{byte})"/> method,
	/// which runs a similar test but also provides the decrypted diversifier index.</para>
	/// </remarks>
	public bool CheckReceiver(OrchardReceiver receiver)
	{
		Span<byte> diversifier = stackalloc byte[11];
		return this.TryGetDiversifierIndex(receiver, diversifier);
	}

	/// <summary>
	/// Checks whether a given orchard receiver was derived from the same spending authority as this key
	/// (in other words: would ZEC sent to this receiver arrive in this account?).
	/// If so, the diversifier that was used to create it is decrypted back into its original index.
	/// </summary>
	/// <param name="receiver">The receiver to decrypt.</param>
	/// <param name="diversifierIndex">Receives the original diversifier index, if successful.</param>
	/// <returns>A value indicating whether the receiver could be decrypted successfully (i.e. the receiver came from this key).</returns>
	/// <remarks>
	/// <para>Use <see cref="CheckReceiver(OrchardReceiver)"/> for a simpler API if the diversifier index is not required.</para>
	/// </remarks>
	public bool TryGetDiversifierIndex(OrchardReceiver receiver, Span<byte> diversifierIndex)
	{
		Span<byte> ivk = stackalloc byte[64];
		if (NativeMethods.GetOrchardIncomingViewingKeyFromFullViewingKey(this.RawEncoding, ivk) != 0)
		{
			throw new InvalidKeyException();
		}

		return NativeMethods.DecryptOrchardDiversifier(ivk, receiver.Span, diversifierIndex) switch
		{
			0 => true,
			1 => false,
			_ => throw new ArgumentException(),
		};
	}

	/// <inheritdoc cref="TryGetDiversifierIndex(OrchardReceiver, Span{byte})"/>
	public bool TryGetDiversifierIndex(OrchardReceiver receiver, [NotNullWhen(true)] out BigInteger? diversifierIndex)
	{
		Span<byte> diversifierIndexBytes = stackalloc byte[11];
		if (this.TryGetDiversifierIndex(receiver, diversifierIndexBytes))
		{
			diversifierIndex = new BigInteger(diversifierIndexBytes, isUnsigned: true);
			return true;
		}
		else
		{
			diversifierIndex = null;
			return false;
		}
	}

	/// <inheritdoc/>
	public bool Equals(FullViewingKey? other)
	{
		return other is not null
			&& this.rawEncoding.Value.SequenceEqual(other.rawEncoding.Value)
			&& this.Network == other.Network;
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
