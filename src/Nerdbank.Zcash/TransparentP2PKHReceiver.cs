// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Transparent"/> pool
/// by way of a Pay to Public Key Hash method.
/// </summary>
public unsafe struct TransparentP2PKHReceiver : IPoolReceiver
{
	private const int Length = 160 / 8;

	private fixed byte validatingKeyHash[Length];

	/// <summary>
	/// Initializes a new instance of the <see cref="TransparentP2PKHReceiver"/> struct.
	/// </summary>
	/// <param name="p2pkh">The validating key hash.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	public TransparentP2PKHReceiver(ReadOnlySpan<byte> p2pkh)
	{
		if (p2pkh.Length != Length)
		{
			throw new ArgumentException($"Length must be exactly {Length}, but was {p2pkh.Length}.", nameof(p2pkh));
		}

		p2pkh.CopyTo(this.ValidatingKeyHashWritable);
	}

	/// <inheritdoc cref="IPoolReceiver.UnifiedReceiverTypeCode"/>
	public static byte UnifiedReceiverTypeCode => 0x02;

	/// <inheritdoc/>
	public readonly Pool Pool => Pool.Transparent;

	/// <summary>
	/// Gets the validating key hash.
	/// </summary>
	public readonly ReadOnlySpan<byte> ValidatingKeyHash => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.validatingKeyHash[0]), Length);

	/// <inheritdoc />
	public readonly ReadOnlySpan<byte> Span => this.ValidatingKeyHash;

	/// <summary>
	/// Gets the validating key hash.
	/// </summary>
	private Span<byte> ValidatingKeyHashWritable => MemoryMarshal.CreateSpan(ref this.validatingKeyHash[0], Length);
}
