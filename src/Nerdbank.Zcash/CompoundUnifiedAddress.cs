// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash;

/// <summary>
/// Contains one or more receivers for a <see cref="UnifiedAddress"/>.
/// </summary>
// TODO: Consider moving the overrides here to the base class and eliminating this class.
public class CompoundUnifiedAddress : UnifiedAddress
{
    private ReadOnlyCollection<ZcashAddress> receivers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompoundUnifiedAddress"/> class.
    /// </summary>
    /// <param name="address">The Unified Address from which this instance was constructed.</param>
    /// <param name="receivers">The embedded receivers in this unified address.</param>
    internal CompoundUnifiedAddress(ReadOnlySpan<char> address, ReadOnlyCollection<ZcashAddress> receivers)
        : base(address)
    {
        this.receivers = receivers;
    }

    /// <summary>
    /// Gets the receivers for this address, in order of preference.
    /// </summary>
    /// <remarks>
    /// <para>Every address has at least one receiver, if it is valid. A <see cref="UnifiedAddress"/> in this sequence should be interpreted as an Orchard raw receiver.</para>
    /// </remarks>
    public IReadOnlyList<ZcashAddress> Receivers => this.receivers;

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
        byte typeCode = TPoolReceiver.UnifiedReceiverTypeCode;
        int length = sizeof(TPoolReceiver);

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
