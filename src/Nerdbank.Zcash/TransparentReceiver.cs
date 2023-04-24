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

    public TransparentP2SHReceiver(ReadOnlySpan<byte> p2sh)
    {
        if (p2sh.Length != Length)
        {
            throw new ArgumentException($"Length must be exactly {Length}.", nameof(p2sh));
        }

        p2sh.CopyTo(this.ScriptHash);
    }

    public Span<byte> ScriptHash => MemoryMarshal.CreateSpan(ref this.scriptHash[0], Length);

    public Span<byte> WholeThing => this.ScriptHash;

    public static byte UnifiedReceiverTypeCode => 0x01;
}

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Transparent"/> pool
/// by way of a Pay to Public Key Hash method.
/// </summary>
public unsafe struct TransparentP2PKHReceiver : IPoolReceiver
{
    private const int Length = 160 / 8;

    private fixed byte validatingKeyHash[Length];

    public TransparentP2PKHReceiver(ReadOnlySpan<byte> p2pkh)
    {
        if (p2pkh.Length != Length)
        {
            throw new ArgumentException($"Length must be exactly {Length}.", nameof(p2pkh));
        }

        p2pkh.CopyTo(this.ValidatingKeyHash);
    }

    public Span<byte> ValidatingKeyHash => MemoryMarshal.CreateSpan(ref this.validatingKeyHash[0], Length);

    public Span<byte> WholeThing => this.ValidatingKeyHash;

    public static byte UnifiedReceiverTypeCode => 0x02;
}

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Sprout"/> pool.
/// </summary>
public unsafe struct SproutReceiver : IPoolReceiver
{
    private const int FieldLength = 256 / 8;
    private const int Length = FieldLength * 2;
    private fixed byte backing[FieldLength * 2];

    public SproutReceiver(ReadOnlySpan<byte> apk, ReadOnlySpan<byte> pkEnc)
    {
        if (apk.Length != FieldLength)
        {
            throw new ArgumentException($"Length must be exactly {FieldLength}.", nameof(apk));
        }

        if (pkEnc.Length != FieldLength)
        {
            throw new ArgumentException($"Length must be exactly {FieldLength}.", nameof(pkEnc));
        }

        apk.CopyTo(this.Apk);
        pkEnc.CopyTo(this.PkEnc);
    }

    internal SproutReceiver(ReadOnlySpan<byte> wholeThing)
    {
        if (wholeThing.Length != Length)
        {
            throw new ArgumentException($"Length must be exactly {Length}.", nameof(wholeThing));
        }

        wholeThing.CopyTo(this.WholeThing);
    }

    public Span<byte> Apk => MemoryMarshal.CreateSpan(ref this.backing[0], FieldLength);

    public Span<byte> PkEnc => MemoryMarshal.CreateSpan(ref this.backing[FieldLength], FieldLength);

    public Span<byte> WholeThing => MemoryMarshal.CreateSpan(ref this.backing[0], Length);

    public static byte UnifiedReceiverTypeCode => throw new NotSupportedException();
}

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Sapling"/> pool.
/// </summary>
public unsafe struct SaplingReceiver : IPoolReceiver
{
    private const int DLength = 88 / 8;
    private const int PkdLength = 256 / 8;
    private const int Length = DLength + PkdLength;
    private fixed byte backing[Length];

    public SaplingReceiver(ReadOnlySpan<byte> d, ReadOnlySpan<byte> pkd)
    {
        if (d.Length != DLength)
        {
            throw new ArgumentException($"Length must be exactly {DLength}.", nameof(d));
        }

        if (pkd.Length != PkdLength)
        {
            throw new ArgumentException($"Length must be exactly {PkdLength}.", nameof(pkd));
        }

        d.CopyTo(this.D);
        pkd.CopyTo(this.Pkd);
    }

    internal SaplingReceiver(ReadOnlySpan<byte> wholeThing)
    {
        if (wholeThing.Length != Length)
        {
            throw new ArgumentException($"Length must be exactly {Length}.", nameof(wholeThing));
        }

        wholeThing.CopyTo(this.WholeThing);
    }

    public Span<byte> D => MemoryMarshal.CreateSpan(ref this.backing[0], DLength);

    public Span<byte> Pkd => MemoryMarshal.CreateSpan(ref this.backing[DLength], PkdLength);

    public Span<byte> WholeThing => MemoryMarshal.CreateSpan(ref this.backing[0], Length);

    public static byte UnifiedReceiverTypeCode => 0x02;
}

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Orchard"/> pool.
/// </summary>
public unsafe struct OrchardReceiver : IPoolReceiver
{
    private const int DLength = 88 / 8;
    private const int PkdLength = 256 / 8;
    private const int Length = DLength + PkdLength;
    private fixed byte backing[Length];

    public OrchardReceiver(ReadOnlySpan<byte> d, ReadOnlySpan<byte> pkd)
    {
        if (d.Length != DLength)
        {
            throw new ArgumentException($"Length must be exactly {DLength}.", nameof(d));
        }

        if (pkd.Length != PkdLength)
        {
            throw new ArgumentException($"Length must be exactly {PkdLength}.", nameof(pkd));
        }

        d.CopyTo(this.D);
        pkd.CopyTo(this.Pkd);
    }

    public Span<byte> D => MemoryMarshal.CreateSpan(ref this.backing[0], DLength);

    public Span<byte> Pkd => MemoryMarshal.CreateSpan(ref this.backing[DLength], PkdLength);

    public Span<byte> WholeThing => MemoryMarshal.CreateSpan(ref this.backing[0], Length);

    public static byte UnifiedReceiverTypeCode => 0x03;
}

/// <summary>
/// An interface implemented by receivers that are embedded in Zcash addresses.
/// </summary>
public interface IPoolReceiver
{
    static abstract byte UnifiedReceiverTypeCode { get; }

    Span<byte> WholeThing { get; }
}
