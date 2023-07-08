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
	/// <param name="isTestNet">A value indicating whether this key is for use on the testnet.</param>
	internal DiversifiableFullViewingKey(in ExpandedSpendingKey spendingKey, DiversifierKey dk, bool isTestNet)
		: base(spendingKey, isTestNet)
	{
		this.Dk = dk;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifiableFullViewingKey"/> class.
	/// </summary>
	/// <param name="fullViewingKey">The full viewing key.</param>
	/// <param name="dk">The diversifier key.</param>
	/// <param name="isTestNet">A value indicating whether this key is for use on the testnet.</param>
	internal DiversifiableFullViewingKey(FullViewingKey fullViewingKey, DiversifierKey dk, bool isTestNet)
		: base(fullViewingKey.ViewingKey, fullViewingKey.Ovk, isTestNet)
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
	/// This value will be changed to match the index at which a diversifier was found.
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

		Span<byte> fvk = stackalloc byte[96];
		this.ToBytes(fvk);

		Span<byte> receiverBytes = stackalloc byte[SaplingReceiver.Length];
		if (NativeMethods.TryGetSaplingReceiver(fvk, this.Dk.Value, indexBytes, receiverBytes) != 0)
		{
			return false;
		}

		// The index may have been changed. Apply that change to our ref parameter.
		index = new BigInteger(indexBytes, isUnsigned: true);

		receiver = new(receiverBytes);

		return true;
	}
}
