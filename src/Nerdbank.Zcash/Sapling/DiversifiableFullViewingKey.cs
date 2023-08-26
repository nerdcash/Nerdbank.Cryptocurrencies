// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.Orchard;

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A viewing key that can decrypt incoming and outgoing transactions
/// and generate addresses.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class DiversifiableFullViewingKey : FullViewingKey, IUnifiedEncodingElement, IEquatable<DiversifiableFullViewingKey>
{
	private readonly DiversifierKey dk;

	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifiableFullViewingKey"/> class.
	/// </summary>
	/// <param name="fullViewingKey">The full viewing key.</param>
	/// <param name="dk">The diversifier key.</param>
	internal DiversifiableFullViewingKey(FullViewingKey fullViewingKey, in DiversifierKey dk)
		: base(fullViewingKey.Ak, fullViewingKey.Nk, fullViewingKey.IncomingViewingKey, fullViewingKey.Ovk)
	{
		this.dk = dk;

		// Replace the base class's value with our own that includes the Dk value.
		this.IncomingViewingKey = new IncomingViewingKey(this.IncomingViewingKey.Ivk.Value, this.Dk.Value, fullViewingKey.Network);
	}

	/// <inheritdoc/>
	byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.Sapling;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.UnifiedDataLength => this.Ak.Value.Length + this.Nk.Value.Length + this.Ovk.Value.Length + this.Dk.Value.Length;

	/// <summary>
	/// Gets the diversifier key.
	/// </summary>
	/// <value>A 32-byte buffer.</value>
	internal ref readonly DiversifierKey Dk => ref this.dk;

	private string DebuggerDisplay => this.IncomingViewingKey.DefaultAddress;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination)
	{
		int written = 0;
		written += this.Ak.Value.CopyToRetLength(destination[written..]);
		written += this.Nk.Value.CopyToRetLength(destination[written..]);
		written += this.Ovk.Value.CopyToRetLength(destination[written..]);
		written += this.Dk.Value.CopyToRetLength(destination[written..]);
		return written;
	}

	/// <summary>
	/// Checks whether a given sapling receiver was derived from the same spending authority as this key
	/// (in other words: would ZEC sent to this receiver arrive in this account?).
	/// </summary>
	/// <param name="receiver">The receiver to test.</param>
	/// <returns><see langword="true"/> if this receiver would send ZEC to this account; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// <para>This is a simpler front-end for the <see cref="TryGetDiversifierIndex(SaplingReceiver, Span{byte})"/> method,
	/// which runs a similar test but also provides the decrypted diversifier index.</para>
	/// </remarks>
	public bool CheckReceiver(SaplingReceiver receiver)
	{
		Span<byte> diversifier = stackalloc byte[11];
		return this.TryGetDiversifierIndex(receiver, diversifier);
	}

	/// <summary>
	/// Checks whether a given sapling receiver was derived from the same spending authority as this key
	/// (in other words: would ZEC sent to this receiver arrive in this account?).
	/// If so, the diversifier that was used to create it is decrypted back into its original index.
	/// </summary>
	/// <param name="receiver">The receiver to decrypt.</param>
	/// <param name="diversifierIndex">Receives the original diversifier index, if successful.</param>
	/// <returns>A value indicating whether the receiver could be decrypted successfully (i.e. the receiver came from this key).</returns>
	/// <remarks>
	/// <para>Use <see cref="CheckReceiver(SaplingReceiver)"/> for a simpler API if the diversifier index is not required.</para>
	/// </remarks>
	public bool TryGetDiversifierIndex(SaplingReceiver receiver, Span<byte> diversifierIndex)
	{
		return NativeMethods.DecryptSaplingDiversifier(this.ToBytes().Value, this.Dk.Value, receiver.Span, diversifierIndex, out _) switch
		{
			0 => true,
			1 => false,
			_ => throw new ArgumentException(),
		};
	}

	/// <inheritdoc cref="TryGetDiversifierIndex(SaplingReceiver, Span{byte})"/>
	public bool TryGetDiversifierIndex(SaplingReceiver receiver, [NotNullWhen(true)] out BigInteger? diversifierIndex)
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
	public bool Equals(DiversifiableFullViewingKey? other)
	{
		return other is not null
			&& base.Equals(other)
			&& this.Dk.Value.SequenceEqual(other.Dk.Value);
	}

	/// <inheritdoc cref="Orchard.FullViewingKey.DecodeUnifiedViewingKeyContribution(ReadOnlySpan{byte}, ZcashNetwork)"/>
	internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network)
	{
		ReadOnlySpan<byte> ak = keyContribution[..32];
		ReadOnlySpan<byte> nk = keyContribution[32..64];
		ReadOnlySpan<byte> ovk = keyContribution[64..96];
		ReadOnlySpan<byte> dk = keyContribution[96..];
		IncomingViewingKey ivk = IncomingViewingKey.FromFullViewingKey(ak, nk, dk, network);
		FullViewingKey fvk = new(new(ak), new(nk), ivk, new(ovk));
		return new DiversifiableFullViewingKey(fvk, new(dk));
	}
}
