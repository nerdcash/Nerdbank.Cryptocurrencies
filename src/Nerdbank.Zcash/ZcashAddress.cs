// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Zcash;

/// <summary>
/// A Zcash address.
/// </summary>
public abstract class ZcashAddress : IEquatable<ZcashAddress>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ZcashAddress"/> class.
    /// </summary>
    /// <param name="address">The address in string form.</param>
    protected ZcashAddress(ReadOnlySpan<char> address)
    {
        this.Address = address.ToString();
    }

    /// <summary>
    /// Gets the network the address belongs to.
    /// </summary>
    /// <exception cref="InvalidAddressException">Thrown if the address is invalid.</exception>
    public abstract ZcashNetwork Network { get; }

    /// <summary>
    /// Gets a value indicating whether the address is valid.
    /// </summary>
    internal bool IsValid => this.CheckValidity();

    /// <summary>
    /// Gets the total length of this address's contribution to a unified address.
    /// </summary>
    internal int UAContributionLength => 1 + CompactSize.GetEncodedLength((ulong)this.ReceiverEncodingLength) + this.ReceiverEncodingLength;

    /// <summary>
    /// Gets the type code to use when embedded in a unified address.
    /// </summary>
    internal abstract byte UnifiedAddressTypeCode { get; }

    /// <summary>
    /// Gets the length of the receiver encoding in a unified address.
    /// </summary>
    internal abstract int ReceiverEncodingLength { get; }

    /// <summary>
    /// Gets the address as a string.
    /// </summary>
    protected string Address { get; }

    /// <summary>
    /// Implicitly casts this address to a string.
    /// </summary>
    /// <param name="address">The address to convert.</param>
    public static implicit operator string(ZcashAddress address) => address.Address;

    /// <summary>
    /// Parse a string of characters as an address.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The parsed address.</returns>
    /// <exception type="ArgumentException">Thrown if the address is invalid.</exception>
    public static ZcashAddress Parse(ReadOnlySpan<char> address)
    {
        if (!TryParse(address, out ZcashAddress? result, out _, out string? errorMessage))
        {
            throw new ArgumentException(errorMessage);
        }

        return result;
    }

    /// <inheritdoc cref="TryParse(ReadOnlySpan{char}, out ZcashAddress?, out ParseError?, out string?)"/>
    public static bool TryParse(ReadOnlySpan<char> address, [NotNullWhen(true)] out ZcashAddress? result) => TryParse(address, out result, out _, out _);

    /// <summary>
    /// Tries to parse a string of characters as an address.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="result">Receives the parsed address.</param>
    /// <param name="errorCode">Receives the error code if parsing fails.</param>
    /// <param name="errorMessage">Receives the error message if the parsing fails.</param>
    /// <returns>A value indicating whether the address parsed to a valid address.</returns>
    public static bool TryParse(ReadOnlySpan<char> address, [NotNullWhen(true)] out ZcashAddress? result, [NotNullWhen(false)] out ParseError? errorCode, [NotNullWhen(false)] out string? errorMessage)
    {
        for (int attempt = 0; ; attempt++)
        {
            switch (attempt)
            {
                case 0:
                    if (TransparentAddress.TryParse(address, out TransparentAddress? tAddr, out errorCode, out errorMessage))
                    {
                        result = tAddr;
                        return true;
                    }

                    break;
                case 1:
                    if (SproutAddress.TryParse(address, out SproutAddress? sproutAddr, out errorCode, out errorMessage))
                    {
                        result = sproutAddr;
                        return true;
                    }

                    break;
                case 2:
                    if (SaplingAddress.TryParse(address, out SaplingAddress? saplingAddr, out errorCode, out errorMessage))
                    {
                        result = saplingAddr;
                        return true;
                    }

                    break;
                case 3:
                    if (UnifiedAddress.TryParse(address, out UnifiedAddress? orchardAddr, out errorCode, out errorMessage))
                    {
                        result = orchardAddr;
                        return true;
                    }

                    break;
                default:
                    result = null;
                    errorCode = ParseError.UnrecognizedAddressType;
                    errorMessage = Strings.UnrecognizedAddress;
                    return false;
            }

            // Any error other than an unrecognized address type is a fatal error.
            if (errorCode != ParseError.UnrecognizedAddressType)
            {
                result = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether this address is or contains an address for the specified pool.
    /// </summary>
    /// <param name="pool">The pool of interest.</param>
    /// <returns><see langword="true" /> if the address can receive funds for the <paramref name="pool"/>; otherwise <see langword="false"/>.</returns>
    public abstract bool SupportsPool(Pool pool);

    /// <summary>
    /// Returns the zcash address.
    /// </summary>
    /// <returns>The address.</returns>
    public override string ToString() => this.Address;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => this.Equals(obj as ZcashAddress);

    /// <inheritdoc/>
    public override int GetHashCode() => this.Address.GetHashCode();

    /// <inheritdoc/>
    public bool Equals(ZcashAddress? other) => this == other || this.Address == other?.Address;

    /// <summary>
    /// Gets the receiver for a particular pool, if embedded in this address.
    /// </summary>
    /// <typeparam name="TPoolReceiver">
    /// <para>The type of receiver to extract.
    /// The type chosen here determines which pool may be sent funds, and by which method.</para>
    /// <para>Possible type arguments here include:</para>
    /// <list type="bullet">
    /// <item><see cref="OrchardReceiver"/></item>
    /// <item><see cref="SaplingReceiver"/></item>
    /// <item><see cref="TransparentP2PKHReceiver"/></item>
    /// <item><see cref="TransparentP2SHReceiver"/></item>
    /// </list>
    /// </typeparam>
    /// <returns>The encoded receiver, or <see langword="null" /> if no receiver of the specified type is embedded in this address.</returns>
    /// <remarks>
    /// For legacy address types (<see cref="TransparentAddress">transparent</see>, <see cref="SproutAddress">sprout</see>, <see cref="SaplingAddress">sapling</see>), only one type of receiver will return a non-<see langword="null" /> result.
    /// For <see cref="UnifiedAddress">unified addresses</see>, several receiver types may produce a result.
    /// </remarks>
    public abstract TPoolReceiver? GetPoolReceiver<TPoolReceiver>()
        where TPoolReceiver : unmanaged, IPoolReceiver;

    /// <summary>
    /// Writes out the encoded receiver for this address.
    /// </summary>
    /// <param name="output">The buffer to receive the encoded receiver.</param>
    /// <returns>The number of bytes written to <paramref name="output"/>.</returns>
    internal abstract int GetReceiverEncoding(Span<byte> output);

    /// <summary>
    /// Writes this address's contribution to a unified address.
    /// </summary>
    /// <param name="destination">The buffer to receive the UA contribution.</param>
    /// <returns>The number of bytes actually written to the buffer.</returns>
    internal int WriteUAContribution(Span<byte> destination)
    {
        int bytesWritten = 0;
        destination[bytesWritten++] = this.UnifiedAddressTypeCode;
        int predictedEncodingLength = this.ReceiverEncodingLength;
        bytesWritten += CompactSize.Encode((ulong)predictedEncodingLength, destination);
        int actualEncodingLength = this.GetReceiverEncoding(destination.Slice(bytesWritten));
        Assumes.True(predictedEncodingLength == actualEncodingLength); // If this is wrong, we encoded the wrong length in the compact size.
        bytesWritten += actualEncodingLength;
        return bytesWritten;
    }

    /// <summary>
    /// Checks whether the address is valid.
    /// </summary>
    /// <param name="throwIfInvalid">A value indicating whether to throw an exception if the address is invalid.</param>
    /// <returns>A value indicating whether the address is invalid.</returns>
    /// <exception cref="FormatException">Thrown if <paramref name="throwIfInvalid"/> is <see langword="true" /> and the address is invalid.</exception>
    protected abstract bool CheckValidity(bool throwIfInvalid = false);

    /// <summary>
    /// Gets the length of the buffer required to call <see cref="WriteUAContribution{TReceiver}(TReceiver, Span{byte})"/>.
    /// </summary>
    /// <typeparam name="TReceiver">The type of receiver to be written.</typeparam>
    /// <returns>The length of the required buffer, in bytes.</returns>
    private protected static unsafe int GetUAContributionLength<TReceiver>()
        where TReceiver : unmanaged, IPoolReceiver
    {
        return 1 + CompactSize.GetEncodedLength((ulong)sizeof(TReceiver)) + sizeof(TReceiver);
    }

    /// <summary>
    /// Writes a receiver's contribution to a unified address.
    /// </summary>
    /// <typeparam name="TReceiver">The type of the receiver to be written.</typeparam>
    /// <param name="receiver">The receiver.</param>
    /// <param name="destination">The buffer to write to.</param>
    /// <returns>The number of bytes actually written.</returns>
    private protected static unsafe int WriteUAContribution<TReceiver>(TReceiver receiver, Span<byte> destination)
        where TReceiver : unmanaged, IPoolReceiver
    {
        int bytesWritten = 0;
        destination[bytesWritten++] = TReceiver.UnifiedReceiverTypeCode;
        bytesWritten += CompactSize.Encode((ulong)receiver.GetSpan().Length, destination);
        Span<byte> receiverSpan = receiver.GetSpan();
        receiverSpan.CopyTo(destination.Slice(bytesWritten));
        bytesWritten += receiverSpan.Length;
        return bytesWritten;
    }

    /// <summary>
    /// Casts one receiver type to another, throwing an exception if the cast is invalid.
    /// </summary>
    /// <typeparam name="TNative">The native receiver type for the calling address.</typeparam>
    /// <typeparam name="TTarget">The generic type parameter provided to the caller, to which the receiver must be cast.</typeparam>
    /// <param name="receiver">The receiver to be cast.</param>
    /// <returns>The re-cast receiver.</returns>
    private protected static TTarget? CastReceiver<TNative, TTarget>(TNative receiver)
        where TNative : unmanaged, IPoolReceiver
        where TTarget : unmanaged, IPoolReceiver
    {
        // If this step boxes the struct, we could fix it with some "unsafe" code.
        return (TTarget)(object)receiver;
    }

    /// <summary>
    /// Casts one receiver type to another if they are compatible, returning <see langword="null" /> if the cast is invalid.
    /// </summary>
    /// <typeparam name="TNative">The native receiver type for the calling address.</typeparam>
    /// <typeparam name="TTarget">The generic type parameter provided to the caller, to which the receiver must be cast.</typeparam>
    /// <param name="receiver">The receiver to be cast.</param>
    /// <returns>The re-cast receiver, or <see langword="null" /> if the types do not match.</returns>
    private protected static TTarget? AsReceiver<TNative, TTarget>(TNative receiver)
        where TNative : unmanaged, IPoolReceiver
        where TTarget : unmanaged, IPoolReceiver
    {
        return typeof(TNative) == typeof(TTarget) ? CastReceiver<TNative, TTarget>(receiver) : null;
    }

    /// <summary>
    /// Translates an internal <see cref="DecodeError"/> to a public <see cref="ParseError"/>.
    /// </summary>
    /// <param name="decodeError">The decode error.</param>
    /// <returns>The parse error to report to the user.</returns>
    [return: NotNullIfNotNull(nameof(decodeError))]
    private protected static ParseError? DecodeToParseError(DecodeError? decodeError)
    {
        return decodeError switch
        {
            null => null,
            DecodeError.BufferTooSmall => throw Assumes.Fail("An internal error occurred: the buffer was too small."),
            _ => ParseError.InvalidAddress,
        };
    }
}
