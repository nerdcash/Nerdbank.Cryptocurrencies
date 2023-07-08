// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

/// <summary>
/// The functions and data types imported from rust.
/// </summary>
internal static unsafe class NativeMethods
{
	private const string LibraryName = "nerdbank_zcash_rust";

	/// <summary>
	/// Derives an Orchard full viewing key from a spending key.
	/// </summary>
	/// <param name="spendingKey">A 32-byte buffer containing the spending key.</param>
	/// <param name="fullViewingKey">A 96 byte buffer that will receive the full viewing key.</param>
	/// <returns>0 if successful; negative for an error code.</returns>
	internal static int TryDeriveOrchardFullViewingKeyFromSpendingKey(ReadOnlySpan<byte> spendingKey, Span<byte> fullViewingKey)
	{
		fixed (byte* sk = spendingKey)
		{
			fixed (byte* fvk = fullViewingKey)
			{
				return NativeMethods.get_orchard_fvk_bytes_from_sk_bytes(sk, fvk);
			}
		}
	}

	/// <summary>
	/// Constructs an Orchard raw payment address from a full viewing key and diversifier.
	/// </summary>
	/// <param name="fullViewingKey">The 96-byte full viewing key.</param>
	/// <param name="diversifier_index">The 11-byte diversifier.</param>
	/// <param name="rawPaymentAddress">The 43-byte buffer that will receive the raw payment address.</param>
	/// <returns>0 if successful; negative for an error code.</returns>
	/// <exception cref="ArgumentException">Thrown if any of the arguments are not of the required lengths.</exception>
	internal static int TryGetOrchardRawPaymentAddress(ReadOnlySpan<byte> fullViewingKey, ReadOnlySpan<byte> diversifier_index, Span<byte> rawPaymentAddress)
	{
		if (fullViewingKey.Length != 96 || rawPaymentAddress.Length != 43)
		{
			throw new ArgumentException();
		}

		fixed (byte* fvk = fullViewingKey)
		{
			fixed (byte* pd = diversifier_index)
			{
				fixed (byte* p = rawPaymentAddress)
				{
					return NativeMethods.get_orchard_raw_payment_address_from_fvk(fvk, pd, p);
				}
			}
		}
	}

	/// <summary>
	/// Gets a sapling full viewing key from an expanded spending key.
	/// </summary>
	/// <param name="expandedSpendingKey">The 96-byte binary representation of the expanded spending key.</param>
	/// <param name="fullViewingKey">The 96-byte buffer that will receive the full viewing key.</param>
	/// <returns>0 if the conversion was successful; otherwise a negative error code.</returns>
	/// <exception cref="ArgumentException">Thrown if the arguments have the wrong length.</exception>
	internal static int TryGetSaplingFullViewingKeyFromExpandedSpendingKey(ReadOnlySpan<byte> expandedSpendingKey, Span<byte> fullViewingKey)
	{
		if (expandedSpendingKey.Length != 96 || fullViewingKey.Length != 96)
		{
			throw new ArgumentException();
		}

		fixed (byte* pExpsk = expandedSpendingKey)
		{
			fixed (byte* pFvk = fullViewingKey)
			{
				return get_sapling_fvk_from_expanded_sk(pExpsk, pFvk);
			}
		}
	}

	/// <summary>
	/// Gets the sapling receiver given a full viewing key.
	/// </summary>
	/// <param name="fullViewingKey">The 96-byte representation of the full viewing key.</param>
	/// <param name="diversifierKey">The 32-byte representation of the diversifier key.</param>
	/// <param name="diversifierIndex">The 11-byte buffer representing the diversifier index.</param>
	/// <param name="receiver">The 43-byte buffer that will be initialized with the receiver.</param>
	/// <returns>0 if successful; otherwise a negative error code.</returns>
	internal static int TryGetSaplingReceiver(ReadOnlySpan<byte> fullViewingKey, ReadOnlySpan<byte> diversifierKey, Span<byte> diversifierIndex, Span<byte> receiver)
	{
		if (fullViewingKey.Length != 96 || diversifierKey.Length != 32 || diversifierIndex.Length != 11 || receiver.Length != 43)
		{
			throw new ArgumentException();
		}

		fixed (byte* fvk = fullViewingKey)
		{
			fixed (byte* dk = diversifierKey)
			{
				fixed (byte* di = diversifierIndex)
				{
					fixed (byte* r = receiver)
					{
						return get_sapling_receiver(fvk, dk, di, r);
					}
				}
			}
		}
	}

	/// <summary>
	/// Gets the expanded spending key (ask, nsk, ovk) from a spending key.
	/// </summary>
	/// <param name="spendingKey">The 32-byte spending key.</param>
	/// <param name="expandedSpendingKey">Receives the 96-byte expanded spending key.</param>
	/// <exception cref="ArgumentException">Thrown if the buffers have the wrong length.</exception>
	internal static void GetSaplingExpandedSpendingKey(ReadOnlySpan<byte> spendingKey, Span<byte> expandedSpendingKey)
	{
		if (spendingKey.Length != 32 || expandedSpendingKey.Length != 96)
		{
			throw new ArgumentException();
		}

		fixed (byte* sk = spendingKey)
		{
			fixed (byte* expsk = expandedSpendingKey)
			{
				get_sapling_expanded_sk(sk, expsk);
			}
		}
	}

	/// <summary>
	/// Derives a child extended key from a sapling extended key.
	/// </summary>
	/// <param name="extendedSpendingKey">The 169-byte buffer containing the existing extended key.</param>
	/// <param name="childIndex">The index of the child to derive.</param>
	/// <param name="childExtendedSpendingKey">The 169-byte buffer to receive the derived child.</param>
	/// <returns>0 if successful; a negative error code otherwise.</returns>
	internal static int DeriveSaplingChild(ReadOnlySpan<byte> extendedSpendingKey, uint childIndex, Span<byte> childExtendedSpendingKey)
	{
		if (extendedSpendingKey.Length != 169 || childExtendedSpendingKey.Length != 169)
		{
			throw new ArgumentException();
		}

		fixed (byte* sk = extendedSpendingKey)
		{
			fixed (byte* child = childExtendedSpendingKey)
			{
				return derive_sapling_child(sk, childIndex, child);
			}
		}
	}

	/// <summary>
	/// Derives an Orchard full viewing key from a spending key.
	/// </summary>
	/// <param name="sk">A pointer to a 32-byte buffer containing the spending key.</param>
	/// <param name="fvk">A pointer to the 96 byte buffer that will receive the full viewing key.</param>
	/// <returns>0 if successful; negative for an error code.</returns>
	[DllImport(LibraryName)]
	private static extern int get_orchard_fvk_bytes_from_sk_bytes(byte* sk, byte* fvk);

	/// <summary>
	/// Constructs an Orchard raw payment address from a full viewing key and diversifier.
	/// </summary>
	/// <param name="fvk">A pointer to the buffer containing the 96-byte full viewing key.</param>
	/// <param name="diversifier_index">A pointer to an 11-byte diversifier.</param>
	/// <param name="raw_payment_address">A pointer to the 43 byte buffer that will receive the raw payment address.</param>
	/// <returns>0 if successful; negative for an error code.</returns>
	[DllImport(LibraryName)]
	private static extern int get_orchard_raw_payment_address_from_fvk(byte* fvk, byte* diversifier_index, byte* raw_payment_address);

	[DllImport(LibraryName)]
	private static extern int get_sapling_fvk_from_expanded_sk(byte* expsk, byte* fvk);

	[DllImport(LibraryName)]
	private static extern int get_sapling_receiver(byte* fullViewingKey, byte* diversifierKey, byte* diversifierIndex, byte* receiver);

	[DllImport(LibraryName)]
	private static extern void get_sapling_expanded_sk(byte* sk, byte* expsk);

	[DllImport(LibraryName)]
	private static extern int derive_sapling_child(byte* extSK, uint child_index, byte* childSK);
}
