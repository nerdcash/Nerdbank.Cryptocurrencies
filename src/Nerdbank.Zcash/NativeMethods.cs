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
	internal static unsafe int TryDeriveOrchardFullViewingKeyFromSpendingKey(ReadOnlySpan<byte> spendingKey, Span<byte> fullViewingKey)
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
	/// <param name="diversifier">The 11-byte diversifier.</param>
	/// <param name="rawPaymentAddress">The 43-byte buffer that will receive the raw payment address.</param>
	/// <returns>0 if successful; negative for an error code.</returns>
	/// <exception cref="ArgumentException">Thrown if any of the arguments are not of the required lengths.</exception>
	internal static unsafe int TryGetOrchardRawPaymentAddress(ReadOnlySpan<byte> fullViewingKey, ReadOnlySpan<byte> diversifier, Span<byte> rawPaymentAddress)
	{
		if (fullViewingKey.Length != 96 || diversifier.Length != 11 || rawPaymentAddress.Length != 43)
		{
			throw new ArgumentException();
		}

		fixed (byte* fvk = fullViewingKey)
		{
			fixed (byte* d = diversifier)
			{
				fixed (byte* p = rawPaymentAddress)
				{
					return NativeMethods.get_orchard_raw_payment_address_from_fvk(fvk, d, p);
				}
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
	/// <param name="d">A pointer to the buffer containing the 11-byte diversifier.</param>
	/// <param name="raw_payment_address">A pointer to the 43 byte buffer that will receive the raw payment address.</param>
	/// <returns>0 if successful; negative for an error code.</returns>
	[DllImport(LibraryName)]
	private static extern int get_orchard_raw_payment_address_from_fvk(byte* fvk, byte* d, byte* raw_payment_address);
}
