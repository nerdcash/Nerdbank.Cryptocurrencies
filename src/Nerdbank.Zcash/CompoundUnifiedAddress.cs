// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash;

/// <summary>
/// Contains one or more receivers for a <see cref="UnifiedAddress"/>.
/// </summary>
// TODO: Consider moving the overrides here to the base class and eliminating this class.
internal class CompoundUnifiedAddress : UnifiedAddress
{
    private ReadOnlyCollection<ZcashAddress> receivers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompoundUnifiedAddress"/> class.
    /// </summary>
    /// <param name="address">The Unified Address from which this instance was constructed.</param>
    /// <param name="receivers">The embedded receivers in this unified address, in order of preference.</param>
    internal CompoundUnifiedAddress(ReadOnlySpan<char> address, ReadOnlyCollection<ZcashAddress> receivers)
        : base(address)
    {
        this.receivers = receivers;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<ZcashAddress> Receivers => this.receivers;

    /// <inheritdoc/>
    internal override int ReceiverEncodingLength
    {
        get
        {
            return this.receivers.Count == 1
                ? this.receivers[0].ReceiverEncodingLength
                : throw new NotSupportedException("This unified address is not a raw receiver address.");
        }
    }

    /// <inheritdoc/>
    internal override byte UnifiedAddressTypeCode
    {
        get
        {
            return this.receivers.Count == 1
                ? this.receivers[0].UnifiedAddressTypeCode
                : throw new NotSupportedException("This unified address is not a raw receiver address and cannot be embedded into another unified address.");
        }
    }

    /// <inheritdoc/>
    public unsafe override TPoolReceiver? GetPoolReceiver<TPoolReceiver>()
    {
        foreach (ZcashAddress receiver in this.receivers)
        {
            if (receiver.GetPoolReceiver<TPoolReceiver>() is TPoolReceiver poolReceiver)
            {
                return poolReceiver;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool) => this.receivers.Any(r => r.SupportsPool(pool));

    /// <inheritdoc/>
    internal override int GetReceiverEncoding(Span<byte> output)
    {
        return this.receivers.Count == 1
            ? this.receivers[0].GetReceiverEncoding(output)
            : throw new NotSupportedException("This unified address is not a raw receiver address.");
    }
}
