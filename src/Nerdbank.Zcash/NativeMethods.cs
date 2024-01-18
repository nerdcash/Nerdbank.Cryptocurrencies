// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Nerdbank.Zcash;

/// <summary>
/// The functions and data types imported from rust.
/// </summary>
internal static unsafe partial class NativeMethods
{
	private const string LibraryName = "nerdbank_zcash_rust";

	/// <summary>
	/// Passes a byte buffer through the Orchard ToScalar method in the spec.
	/// </summary>
	/// <param name="uniformBytes">A 64-byte buffer.</param>
	/// <param name="repr">A 32-byte buffer to receive the processed bytes.</param>
	/// <returns>A return code.</returns>
	internal static int OrchardToScalarToRepr(ReadOnlySpan<byte> uniformBytes, Span<byte> repr)
	{
		if (uniformBytes.Length != 64 || repr.Length != 32)
		{
			throw new ArgumentException();
		}

		fixed (byte* pUniformBytes = uniformBytes)
		{
			fixed (byte* pRepr = repr)
			{
				return orchard_to_scalar_to_repr(pUniformBytes, pRepr);
			}
		}
	}

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
				return get_orchard_fvk_bytes_from_sk_bytes(sk, fvk);
			}
		}
	}

	/// <summary>
	/// Constructs an Orchard raw payment address from an incoming viewing key and diversifier.
	/// </summary>
	/// <param name="incomingViewingKey">The 64-byte incoming viewing key.</param>
	/// <param name="diversifier_index">The 11-byte diversifier.</param>
	/// <param name="rawPaymentAddress">The 43-byte buffer that will receive the raw payment address.</param>
	/// <returns>0 if successful; negative for an error code.</returns>
	/// <exception cref="ArgumentException">Thrown if any of the arguments are not of the required lengths.</exception>
	internal static int TryGetOrchardRawPaymentAddress(ReadOnlySpan<byte> incomingViewingKey, ReadOnlySpan<byte> diversifier_index, Span<byte> rawPaymentAddress)
	{
		if (incomingViewingKey.Length != 64 || rawPaymentAddress.Length != 43)
		{
			throw new ArgumentException();
		}

		fixed (byte* fvk = incomingViewingKey)
		{
			fixed (byte* pd = diversifier_index)
			{
				fixed (byte* p = rawPaymentAddress)
				{
					return get_orchard_raw_payment_address_from_ivk(fvk, pd, p);
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
	/// Gets the sapling receiver given an incoming viewing key.
	/// </summary>
	/// <param name="incomingViewingKey">The 32-byte representation of the incoming viewing key.</param>
	/// <param name="diversifierKey">The 32-byte representation of the diversifier key.</param>
	/// <param name="diversifierIndex">The 11-byte buffer representing the diversifier index.</param>
	/// <param name="receiver">The 43-byte buffer that will be initialized with the receiver.</param>
	/// <returns>0 if successful; otherwise a negative error code.</returns>
	internal static int TryGetSaplingReceiver(ReadOnlySpan<byte> incomingViewingKey, ReadOnlySpan<byte> diversifierKey, Span<byte> diversifierIndex, Span<byte> receiver)
	{
		if (incomingViewingKey.Length != 32 || diversifierKey.Length != 32 || diversifierIndex.Length != 11 || receiver.Length != 43)
		{
			throw new ArgumentException();
		}

		fixed (byte* ivk = incomingViewingKey)
		{
			fixed (byte* dk = diversifierKey)
			{
				fixed (byte* di = diversifierIndex)
				{
					fixed (byte* r = receiver)
					{
						return get_sapling_receiver(ivk, dk, di, r);
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
	/// Derives a full viewing key into an incoming viewing key.
	/// </summary>
	/// <param name="fullViewingKey">The 96-byte encoding of the full viewing key.</param>
	/// <param name="incomingViewingKey">The 64-byte buffer that will receive the incoming viewing key.</param>
	/// <returns>0 if successful; otherwise a negative error code.</returns>
	internal static int GetOrchardIncomingViewingKeyFromFullViewingKey(ReadOnlySpan<byte> fullViewingKey, Span<byte> incomingViewingKey)
	{
		if (fullViewingKey.Length != 96 || incomingViewingKey.Length != 64)
		{
			throw new ArgumentException();
		}

		fixed (byte* fvk = fullViewingKey)
		{
			fixed (byte* ivk = incomingViewingKey)
			{
				return get_orchard_ivk_from_fvk(fvk, ivk);
			}
		}
	}

	/// <summary>
	/// Tries to decrypt an <see cref="OrchardReceiver"/>'s diversifier back into the diversifier index used to create it.
	/// </summary>
	/// <param name="incomingViewingKey">The 64-byte encoding of the <see cref="Nerdbank.Zcash.Orchard.IncomingViewingKey"/>.</param>
	/// <param name="orchardReceiver">The 43-byte encoding of the <see cref="OrchardReceiver"/>.</param>
	/// <param name="diversifierIndex">The 11-byte buffer that will receive the decrypted diversifier index, if successful.</param>
	/// <returns>0 if successful; 1 if the receiver was not created with this incoming viewing key; a negative number for other errors (e.g. invalid data.)</returns>
	internal static int DecryptOrchardDiversifier(ReadOnlySpan<byte> incomingViewingKey, ReadOnlySpan<byte> orchardReceiver, Span<byte> diversifierIndex)
	{
		if (incomingViewingKey.Length != 64 || orchardReceiver.Length != 43 || diversifierIndex.Length != 11)
		{
			throw new ArgumentException();
		}

		fixed (byte* ivk = incomingViewingKey)
		{
			fixed (byte* receiver = orchardReceiver)
			{
				fixed (byte* dI = diversifierIndex)
				{
					return decrypt_orchard_diversifier(ivk, receiver, dI);
				}
			}
		}
	}

	/// <summary>
	/// Tries to decrypt a <see cref="SaplingReceiver"/>'s diversifier back into the diversifier index used to create it.
	/// </summary>
	/// <param name="fullViewingKey">The 96-byte encoding of a <see cref="Nerdbank.Zcash.Sapling.FullViewingKey"/>.</param>
	/// <param name="diversifierKey">The 32-byte diversifier key.</param>
	/// <param name="saplingReceiver">The 43-byte encoding of the <see cref="SaplingReceiver"/>.</param>
	/// <param name="diversifierIndex">The 11-byte buffer that will receive the decrypted diversifier index, if successful.</param>
	/// <param name="scope">Receives 0 for an externally scoped diversifier; 1 for an internally scoped diversifier.</param>
	/// <returns>0 if successful; 1 if the receiver was not created with this incoming viewing key; a negative number for other errors (e.g. invalid data.)</returns>
	internal static int DecryptSaplingDiversifier(ReadOnlySpan<byte> fullViewingKey, ReadOnlySpan<byte> diversifierKey, ReadOnlySpan<byte> saplingReceiver, Span<byte> diversifierIndex, out byte scope)
	{
		if (fullViewingKey.Length != 96 || diversifierKey.Length != 32 || saplingReceiver.Length != 43 || diversifierIndex.Length != 11)
		{
			throw new ArgumentException();
		}

		fixed (byte* fvk = fullViewingKey)
		{
			fixed (byte* dk = diversifierKey)
			{
				fixed (byte* receiver = saplingReceiver)
				{
					fixed (byte* di = diversifierIndex)
					{
						return decrypt_sapling_diversifier(fvk, dk, receiver, di, out scope);
					}
				}
			}
		}
	}

	/// <summary>
	/// Tries to decrypt a <see cref="SaplingReceiver"/>'s diversifier back into the diversifier index used to create it.
	/// </summary>
	/// <param name="incomingViewingKey">The 96-byte encoding of a <see cref="Nerdbank.Zcash.Sapling.FullViewingKey"/>.</param>
	/// <param name="diversifierKey">The 32-byte diversifier key.</param>
	/// <param name="saplingReceiver">The 43-byte encoding of the <see cref="SaplingReceiver"/>.</param>
	/// <param name="diversifierIndex">The 11-byte buffer that will receive the decrypted diversifier index, if successful.</param>
	/// <returns>0 if successful; 1 if the receiver was not created with this incoming viewing key; a negative number for other errors (e.g. invalid data.)</returns>
	/// <remarks>
	/// Only matches with external-derived keys will be found with an IVK. To match on internal-derived keys, use <see cref="DecryptSaplingDiversifier"/>.
	/// </remarks>
	internal static int DecryptSaplingDiversifierWithIvk(ReadOnlySpan<byte> incomingViewingKey, ReadOnlySpan<byte> diversifierKey, ReadOnlySpan<byte> saplingReceiver, Span<byte> diversifierIndex)
	{
		if (incomingViewingKey.Length != 32 || diversifierKey.Length != 32 || saplingReceiver.Length != 43 || diversifierIndex.Length != 11)
		{
			throw new ArgumentException();
		}

		fixed (byte* ivk = incomingViewingKey)
		{
			fixed (byte* dk = diversifierKey)
			{
				fixed (byte* receiver = saplingReceiver)
				{
					fixed (byte* di = diversifierIndex)
					{
						return decrypt_sapling_diversifier_with_ivk(ivk, dk, receiver, di);
					}
				}
			}
		}
	}

	/// <summary>
	/// Derives the ivk value for a sapling incoming viewing key from elements of the full viewing key.
	/// </summary>
	/// <param name="ak">The <see cref="Nerdbank.Zcash.Sapling.FullViewingKey.Ak"/> value.</param>
	/// <param name="nk">The <see cref="Nerdbank.Zcash.Sapling.FullViewingKey.Nk"/> value.</param>
	/// <param name="ivk">Receives the computed value for <see cref="Nerdbank.Zcash.Sapling.IncomingViewingKey.Ivk"/>.</param>
	/// <returns>0 on success, or a negative error code.</returns>
	/// <exception cref="ArgumentException">Thrown if the provided buffers are not of expected length.</exception>
	internal static int DeriveSaplingIncomingViewingKeyFromFullViewingKey(ReadOnlySpan<byte> fvk, Span<byte> ivk)
	{
		if (fvk.Length != 96 || ivk.Length != 32)
		{
			throw new ArgumentException();
		}

		fixed (byte* pFvk = fvk)
		{
			fixed (byte* pIvk = ivk)
			{
				return derive_sapling_ivk_from_fvk(pFvk, pIvk);
			}
		}
	}

	/// <summary>
	/// Derives the ivk value for a sapling incoming viewing key from elements of the full viewing key.
	/// </summary>
	/// <param name="fvk">The encoding of the public facing <see cref="Nerdbank.Zcash.Sapling.FullViewingKey"/>.</param>
	/// <param name="dk">The encoding of the public facing <see cref="DiversifierKey"/> associated with the public full viewing key.</param>
	/// <param name="internalFvk">Receives the encoded internal <see cref="Nerdbank.Zcash.Sapling.FullViewingKey"/>.</param>
	/// <param name="internalDk">Receives the encoded internal <see cref="DiversifierKey"/>.</param>
	/// <returns>0 on success, or a negative error code.</returns>
	/// <exception cref="ArgumentException">Thrown if the provided buffers are not of expected length.</exception>
	internal static int DeriveSaplingInternalFullViewingKey(ReadOnlySpan<byte> fvk, ReadOnlySpan<byte> dk, Span<byte> internalFvk, Span<byte> internalDk)
	{
		if (fvk.Length != 96 || dk.Length != 32 || internalFvk.Length != 96 || internalDk.Length != 32)
		{
			throw new ArgumentException();
		}

		fixed (byte* pFvk = fvk)
		{
			fixed (byte* pDk = dk)
			{
				fixed (byte* pInternalFvk = internalFvk)
				{
					fixed (byte* pInternalDk = internalDk)
					{
						return derive_internal_fvk_sapling(pFvk, pDk, pInternalFvk, pInternalDk);
					}
				}
			}
		}
	}

	/// <summary>
	/// Derives the ivk value for a sapling incoming viewing key from elements of the full viewing key.
	/// </summary>
	/// <param name="extendedSpendingKey">The encoding of the public facing <see cref="Zip32HDWallet.Sapling.ExtendedSpendingKey"/>.</param>
	/// <param name="internalExtendedSpendingKey">Receives the encoded internal <see cref="Zip32HDWallet.Sapling.ExtendedSpendingKey"/>.</param>
	/// <returns>0 on success, or a negative error code.</returns>
	/// <exception cref="ArgumentException">Thrown if the provided buffers are not of expected length.</exception>
	internal static int DeriveSaplingInternalSpendingKey(ReadOnlySpan<byte> extendedSpendingKey, Span<byte> internalExtendedSpendingKey)
	{
		if (extendedSpendingKey.Length != 169 || internalExtendedSpendingKey.Length != 169)
		{
			throw new ArgumentException();
		}

		fixed (byte* pExtSK = extendedSpendingKey)
		{
			fixed (byte* pInternalExtSK = internalExtendedSpendingKey)
			{
				return derive_internal_sk_sapling(pExtSK, pInternalExtSK);
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
	/// Constructs an Orchard raw payment address from an incoming viewing key and diversifier.
	/// </summary>
	/// <param name="ivk">A pointer to the buffer containing the 64-byte incoming viewing key.</param>
	/// <param name="diversifier_index">A pointer to an 11-byte diversifier.</param>
	/// <param name="raw_payment_address">A pointer to the 43 byte buffer that will receive the raw payment address.</param>
	/// <returns>0 if successful; negative for an error code.</returns>
	[DllImport(LibraryName)]
	private static extern int get_orchard_raw_payment_address_from_ivk(byte* ivk, byte* diversifier_index, byte* raw_payment_address);

	[DllImport(LibraryName)]
	private static extern int get_sapling_fvk_from_expanded_sk(byte* expsk, byte* fvk);

	[DllImport(LibraryName)]
	private static extern int get_sapling_receiver(byte* incomingViewingKey, byte* diversifierKey, byte* diversifierIndex, byte* receiver);

	[DllImport(LibraryName)]
	private static extern void get_sapling_expanded_sk(byte* sk, byte* expsk);

	[DllImport(LibraryName)]
	private static extern int derive_sapling_child(byte* extSK, uint child_index, byte* childSK);

	[DllImport(LibraryName)]
	private static extern int get_orchard_ivk_from_fvk(byte* fvk, byte* ivk);

	[DllImport(LibraryName)]
	private static extern int decrypt_orchard_diversifier(byte* ivk, byte* receiver, byte* diversifier_index);

	[DllImport(LibraryName)]
	private static extern int decrypt_sapling_diversifier(byte* fvk, byte* dk, byte* receiver, byte* diversifier_index, out byte scope);

	[DllImport(LibraryName)]
	private static extern int decrypt_sapling_diversifier_with_ivk(byte* ivk, byte* dk, byte* receiver, byte* diversifier_index);

	[DllImport(LibraryName)]
	private static extern int derive_sapling_ivk_from_fvk(byte* fvk, byte* ivk);

	[DllImport(LibraryName)]
	private static extern int derive_internal_fvk_sapling(byte* fvk, byte* dk, byte* internal_fvk, byte* internal_dk);

	[DllImport(LibraryName)]
	private static extern int derive_internal_sk_sapling(byte* ext_sk, byte* internal_ext_sk);

	[DllImport(LibraryName)]
	private static extern int orchard_to_scalar_to_repr(byte* uniform_bytes, byte* repr);
}
