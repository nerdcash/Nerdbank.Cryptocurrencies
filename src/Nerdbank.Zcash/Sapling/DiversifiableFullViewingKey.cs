// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A viewing key that can decrypt incoming and outgoing transactions
/// and generate addresses.
/// </summary>
public class DiversifiableFullViewingKey : FullViewingKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifiableFullViewingKey"/> class.
	/// </summary>
	/// <param name="spendingKey">The expanded spending key.</param>
	/// <param name="dk">The diversifier key, which allows for generating many addresses that send funds to the same spending authority.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal DiversifiableFullViewingKey(in ExpandedSpendingKey spendingKey, DiversifierKey dk, ZcashNetwork network)
		: base(spendingKey, network)
	{
		this.Dk = dk;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifiableFullViewingKey"/> class.
	/// </summary>
	/// <param name="fullViewingKey">The full viewing key.</param>
	/// <param name="dk">The diversifier key.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal DiversifiableFullViewingKey(FullViewingKey fullViewingKey, DiversifierKey dk, ZcashNetwork network)
		: base(fullViewingKey.ViewingKey, fullViewingKey.Ovk, network)
	{
		this.Dk = dk;
	}

	/// <summary>
	/// Gets the diversifier key.
	/// </summary>
	/// <value>A 32-byte buffer.</value>
	internal DiversifierKey Dk { get; }

	/// <summary>
	/// Creates a sapling receiver using this key and a given diversifier.
	/// </summary>
	/// <param name="index">
	/// The diversifier index to start searching at, in the range of 0..(2^88 - 1).
	/// Not every index will produce a valid diversifier. About half will fail.
	/// The default diversifier is defined as the smallest non-negative index that produces a valid diversifier.
	/// This value will be incremented until a diversifier can be found.
	/// </param>
	/// <param name="receiver">Receives the sapling receiver, if successful.</param>
	/// <returns>
	/// <see langword="true"/> if a valid diversifier could be produced at or above the initial value given by <paramref name="index"/>.
	/// <see langword="false"/> if no valid diversifier could be found at or above <paramref name="index"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is negative.</exception>
	public bool TryCreateReceiver(ref BigInteger index, out SaplingReceiver receiver)
	{
		Requires.Range(index >= 0, nameof(index));

		Span<byte> indexBytes = stackalloc byte[11];
		if (!index.TryWriteBytes(indexBytes, out _, isUnsigned: true))
		{
			throw new ArgumentException("Index must fit within 11 bytes.");
		}

		bool result = this.TryCreateReceiver(indexBytes, out receiver);

		if (result)
		{
			// The index may have been changed. Apply that change to our ref parameter.
			index = new BigInteger(indexBytes, isUnsigned: true);
		}

		return result;
	}

	/// <summary>
	/// Creates a sapling receiver using this key and a given diversifier.
	/// </summary>
	/// <param name="diversifierIndex">
	/// The diversifier index to start searching at, in the range of 0..(2^88 - 1).
	/// Not every index will produce a valid diversifier. About half will fail.
	/// The default diversifier is defined as the smallest non-negative index that produces a valid diversifier.
	/// This value will be incremented until a diversifier can be found, considering the buffer to be a little-endian encoded integer.
	/// </param>
	/// <param name="receiver">Receives the sapling receiver, if successful.</param>
	/// <returns>
	/// <see langword="true"/> if a valid diversifier could be produced at or above the initial value given by <paramref name="diversifierIndex"/>.
	/// <see langword="false"/> if no valid diversifier could be found at or above <paramref name="diversifierIndex"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="diversifierIndex"/> is negative.</exception>
	public bool TryCreateReceiver(Span<byte> diversifierIndex, out SaplingReceiver receiver)
	{
		Span<byte> fvk = stackalloc byte[96];
		this.ToBytes(fvk);

		Span<byte> receiverBytes = stackalloc byte[SaplingReceiver.Length];
		if (NativeMethods.TryGetSaplingReceiver(fvk, this.Dk.Value, diversifierIndex, receiverBytes) != 0)
		{
			return false;
		}

		receiver = new(receiverBytes);
		return true;
	}

	/// <summary>
	/// Creates the default sapling receiver for this key.
	/// </summary>
	/// <returns>The receiver.</returns>
	public SaplingReceiver CreateDefaultReceiver()
	{
		Span<byte> diversifier = stackalloc byte[11];
		Assumes.True(this.TryCreateReceiver(diversifier, out SaplingReceiver receiver));
		return receiver;
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
}
