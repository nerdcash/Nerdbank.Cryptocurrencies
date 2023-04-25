// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Sapling"/> pool.
/// </summary>
public unsafe struct SaplingReceiver : IPoolReceiver
{
    private const int DLength = 88 / 8;
    private const int PkdLength = 256 / 8;
    private const int Length = DLength + PkdLength;
    private fixed byte backing[Length];

    /// <summary>
    /// Initializes a new instance of the <see cref="SaplingReceiver"/> struct.
    /// </summary>
    /// <param name="d">The LEBS2OSP(d) on the receiver.</param>
    /// <param name="pkd">The LEBS2OSP(pkd) on the receiver.</param>
    /// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
    public SaplingReceiver(ReadOnlySpan<byte> d, ReadOnlySpan<byte> pkd)
    {
        if (d.Length != DLength)
        {
            throw new ArgumentException($"Length must be exactly {DLength}, but was {d.Length}.", nameof(d));
        }

        if (pkd.Length != PkdLength)
        {
            throw new ArgumentException($"Length must be exactly {PkdLength}, but was {pkd.Length}.", nameof(pkd));
        }

        d.CopyTo(this.D);
        pkd.CopyTo(this.Pkd);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SaplingReceiver"/> struct.
    /// </summary>
    /// <param name="receiver">The entire content of the receiver.</param>
    /// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
    internal SaplingReceiver(ReadOnlySpan<byte> receiver)
    {
        if (receiver.Length != Length)
        {
            throw new ArgumentException($"Length must be exactly {Length}, but was {receiver.Length}.", nameof(receiver));
        }

        receiver.CopyTo(this.GetSpan());
    }

    /// <inheritdoc cref="IPoolReceiver.UnifiedReceiverTypeCode"/>
    public static byte UnifiedReceiverTypeCode => 0x02;

    /// <summary>
    /// Gets the LEBS2OSP(d) on the receiver.
    /// </summary>
    public Span<byte> D => MemoryMarshal.CreateSpan(ref this.backing[0], DLength);

    /// <summary>
    /// Gets the LEBS2OSP(repr(pkd)) on the receiver.
    /// </summary>
    public Span<byte> Pkd => MemoryMarshal.CreateSpan(ref this.backing[DLength], PkdLength);
}
