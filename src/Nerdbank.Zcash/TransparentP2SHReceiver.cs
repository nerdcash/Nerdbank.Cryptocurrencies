// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Transparent"/> pool
/// by way of a Pay to Script Hash method.
/// </summary>
public unsafe struct TransparentP2SHReceiver : IPoolReceiver
{
    private const int Length = 160 / 8;

    private fixed byte scriptHash[Length];

    /// <summary>
    /// Initializes a new instance of the <see cref="TransparentP2SHReceiver"/> struct.
    /// </summary>
    /// <param name="p2sh">The script hash.</param>
    /// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
    public TransparentP2SHReceiver(ReadOnlySpan<byte> p2sh)
    {
        if (p2sh.Length != Length)
        {
            throw new ArgumentException($"Length must be exactly {Length}.", nameof(p2sh));
        }

        p2sh.CopyTo(this.ScriptHash);
    }

    /// <summary>
    /// Gets a span over the whole receiver.
    /// </summary>
    /// <inheritdoc cref="IPoolReceiver.UnifiedReceiverTypeCode"/>
    public static byte UnifiedReceiverTypeCode => 0x01;

    /// <summary>
    /// Gets the script hash.
    /// </summary>
    public Span<byte> ScriptHash => MemoryMarshal.CreateSpan(ref this.scriptHash[0], Length);
}
