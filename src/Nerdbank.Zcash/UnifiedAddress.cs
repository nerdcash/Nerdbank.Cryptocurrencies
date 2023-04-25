// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Isopoh.Cryptography.Blake2b;

namespace Nerdbank.Zcash;

/// <summary>
/// A <see href="https://zips.z.cash/zip-0316">unified Zcash address</see>.
/// </summary>
public abstract class UnifiedAddress : ZcashAddress
{
    /// <summary>
    /// The human-readable part of a Unified Address.
    /// </summary>
    private protected const string HumanReadablePart = "u";

    /// <summary>
    /// The shortest allowed length of the input to the <see cref="F4Jumble"/> function.
    /// </summary>
    private protected const int MinF4JumbleInputLength = 48;

    /// <summary>
    /// The longest allowed length of the input to the <see cref="F4Jumble"/> function.
    /// </summary>
    private protected const int MaxF4JumbleInputLength = 4194368;

    private const int F4OutputLength = 64; // known in the spec as ℒᵢ

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress.ZcashAddress(ReadOnlySpan{char})" path="/param"/></param>
    internal UnifiedAddress(ReadOnlySpan<char> address)
        : base(address)
    {
    }

    /// <inheritdoc/>
    public override ZcashNetwork Network => ZcashNetwork.MainNet;

    /// <summary>
    /// Gets the receivers for this address, in order of preference.
    /// </summary>
    /// <remarks>
    /// <para>Every address has at least one receiver, if it is valid. A <see cref="UnifiedAddress"/> in this sequence should be interpreted as an Orchard raw receiver.</para>
    /// </remarks>
    public abstract IReadOnlyList<ZcashAddress> Receivers { get; }

    /// <summary>
    /// Gets the padding bytes that must be present in a unified address.
    /// </summary>
    protected static ReadOnlySpan<byte> Padding => "u\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0"u8;

    /// <summary>
    /// Creates a unified address from a list of receiver addresses.
    /// </summary>
    /// <param name="receivers">
    /// The receivers to build into the unified address.
    /// These will be sorted by preferred order before being encoded into the address.
    /// No more than one of each type of address is allowed.
    /// Sprout addresses are not allowed.
    /// </param>
    /// <returns>A unified address that contains all the receivers.</returns>
    public static UnifiedAddress Create(IReadOnlyCollection<ZcashAddress> receivers)
    {
        Requires.NotNull(receivers);
        Requires.Argument(receivers.Count > 0, nameof(receivers), "Cannot create a unified address with no receivers.");

        if (receivers.Count == 1 && receivers.Single() is UnifiedAddress existingUnifiedAddress)
        {
            // If the only receiver is a UA, just return it.
            return existingUnifiedAddress;
        }

        SortedDictionary<byte, ZcashAddress> sortedReceiversByTypeCode = new();
        int totalLength = 0;

        bool hasShieldedAddress = false;
        foreach (ZcashAddress receiver in receivers)
        {
            try
            {
                hasShieldedAddress |= receiver.UnifiedAddressTypeCode > 0x01;
            }
            catch (NotSupportedException ex)
            {
                throw new ArgumentException("Unified Addresses with multiple receivers cannot be specified as one receiver in another UA.", ex);
            }

            if (!sortedReceiversByTypeCode.TryAdd(receiver.UnifiedAddressTypeCode, receiver))
            {
                throw new ArgumentException($"Only one of each type of address is allowed, but more than one {receiver.GetType().Name} was specified.", nameof(receivers));
            }

            totalLength += receiver.UAContributionLength;
        }

        Requires.Argument(hasShieldedAddress, nameof(receivers), "At least one shielded address is required.");

        totalLength += Padding.Length;
        Span<byte> ua = stackalloc byte[totalLength];
        int uaBytesWritten = 0;
        foreach (ZcashAddress receiver in sortedReceiversByTypeCode.Values)
        {
            uaBytesWritten += receiver.WriteUAContribution(ua.Slice(uaBytesWritten));
        }

        Padding.CopyTo(ua.Slice(uaBytesWritten));
        uaBytesWritten += Padding.Length;
        F4Jumble(ua);

        Assumes.True(uaBytesWritten == ua.Length);

        Span<char> result = stackalloc char[Bech32.GetEncodedLength(HumanReadablePart.Length, ua.Length)];
        int finalLength = Bech32.Bech32m.Encode(HumanReadablePart, ua, result);
        Assumes.True(result.Length == finalLength);

        return new CompoundUnifiedAddress(result.Slice(0, finalLength), new(GetReceiversInPreferredOrder(sortedReceiversByTypeCode.Values)));
    }

    /// <inheritdoc cref="ZcashAddress.TryParse(ReadOnlySpan{char}, out ZcashAddress?, out ParseError?, out string?)" />
    internal static bool TryParse(ReadOnlySpan<char> address, [NotNullWhen(true)] out UnifiedAddress? result, [NotNullWhen(false)] out ParseError? errorCode, [NotNullWhen(false)] out string? errorMessage)
    {
        if (!address.StartsWith("u1"))
        {
            errorCode = ParseError.UnrecognizedAddressType;
            errorMessage = Strings.UnrecognizedAddress;
            result = null;
            return false;
        }

        (int Tag, int Data)? length = Bech32.GetDecodedLength(address);
        if (length is null)
        {
            errorCode = ParseError.UnrecognizedAddressType;
            errorMessage = Strings.UnrecognizedAddress;
            result = null;
            return false;
        }

        Span<char> humanReadablePart = stackalloc char[length.Value.Tag];
        Span<byte> data = stackalloc byte[length.Value.Data];
        if (!Bech32.Bech32m.TryDecode(address, humanReadablePart, data, out DecodeError? decodeError, out errorMessage, out _))
        {
            errorCode = DecodeToParseError(decodeError);
            result = null;
            return false;
        }

        if (!humanReadablePart.SequenceEqual(HumanReadablePart))
        {
            errorCode = ParseError.UnrecognizedAddressType;
            errorMessage = Strings.UnrecognizedAddress;
            result = null;
            return false;
        }

        if (length.Value.Data is < MinF4JumbleInputLength or > MaxF4JumbleInputLength)
        {
            errorCode = ParseError.InvalidAddress;
            errorMessage = Strings.InvalidAddressLength;
            result = null;
            return false;
        }

        F4Jumble(data, inverted: true);

        // Verify the 16-byte padding is as expected.
        if (!data[^Padding.Length..].SequenceEqual(Padding))
        {
            errorCode = ParseError.InvalidAddress;
            errorMessage = Strings.InvalidPadding;
            result = null;
            return false;
        }

        // Strip the padding.
        data = data[..^Padding.Length];

        // Walk over each receiver.
        List<ZcashAddress> receiverAddresses = new();
        while (data.Length > 0)
        {
            byte typeCode = data[0];
            data = data[1..];
            data = data[CompactSize.Decode(data, out ulong receiverLengthUL)..];
            int receiverLength = checked((int)receiverLengthUL);

            // Process each receiver type we support, and quietly ignore any we don't.
            ReadOnlySpan<byte> receiverData = data[..receiverLength];
            switch (typeCode)
            {
                case 0x00:
                    receiverAddresses.Add(new TransparentP2PKHAddress(new TransparentP2PKHReceiver(receiverData)));
                    break;
                case 0x01:
                    receiverAddresses.Add(new TransparentP2SHAddress(new TransparentP2SHReceiver(receiverData)));
                    break;
                case 0x02:
                    receiverAddresses.Add(new SaplingAddress(new SaplingReceiver(receiverData)));
                    break;
                case 0x03:
                    receiverAddresses.Add(new OrchardAddress(new OrchardReceiver(receiverData)));
                    break;
            }

            // Move on to the next receiver.
            data = data[receiverLength..];
        }

        // If we parsed exactly one Orchard receiver, just return it as its own address.
        errorCode = null;
        errorMessage = null;
        result = receiverAddresses.Count == 1 && receiverAddresses[0] is OrchardAddress orchardAddr
            ? orchardAddr
            : new CompoundUnifiedAddress(address, new(GetReceiversInPreferredOrder(receiverAddresses)));
        return true;
    }

    /// <summary>
    /// Applies the F4Jumble function to the specified buffer.
    /// </summary>
    /// <param name="ua">The buffer to mutate.</param>
    /// <param name="inverted"><see langword="true" /> to reverse the process.</param>
    /// <exception cref="ArgumentException">Thrown when the input buffer is shorter than <see cref="MinF4JumbleInputLength"/> or longer than <see cref="MaxF4JumbleInputLength"/>.</exception>
    /// <devremarks>
    /// <see href="https://docs.rs/f4jumble/latest/src/f4jumble/lib.rs.html#208">Some source for inspiration</see> while interpreting the spec.
    /// </devremarks>
    protected static void F4Jumble(Span<byte> ua, bool inverted = false)
    {
        if (ua.Length is < MinF4JumbleInputLength or > MaxF4JumbleInputLength)
        {
            throw new ArgumentException($"The UA cannot exceed {MaxF4JumbleInputLength} bytes.", nameof(ua));
        }

        byte[] arrayBuffer = new byte[ua.Length];
        ua.CopyTo(arrayBuffer);

        byte leftLength = (byte)Math.Min(F4OutputLength, ua.Length / 2);
        int rightLength = ua.Length - leftLength;

        if (inverted)
        {
            RoundH(1);
            RoundG(1);
            RoundH(0);
            RoundG(0);
        }
        else
        {
            RoundG(0);
            RoundH(0);
            RoundG(1);
            RoundH(1);
        }

        arrayBuffer.CopyTo(ua);

        // TODO: Several opportunities below to reduce allocations.
        // 1. Use a single HMACBlake2B instance for G and another for H, and just change the key.
        // 2. Re-implement blake2 or find another library that doesn't allocate a new array for each hash.
        // 3. Reuse the Personalize G/H arrays by just mutating them as needed.
        void RoundG(byte i)
        {
            ushort top = checked((ushort)CeilDiv(rightLength, F4OutputLength));
            for (ushort j = 0; j < top; j++)
            {
                Blake2BConfig cfg = new()
                {
                    Personalization = PersonalizeG(i, j),
                    OutputSizeInBytes = F4OutputLength,
                    LockMemoryPolicy = LockMemoryPolicy.None,
                };
                byte[] hash = Blake2B.ComputeHash(arrayBuffer, 0, leftLength, cfg, null!);
                Xor(arrayBuffer.AsSpan(leftLength + (j * F4OutputLength)), hash);
            }
        }

        void RoundH(byte i)
        {
            Blake2BConfig cfg = new()
            {
                Personalization = PersonalizeH(i),
                OutputSizeInBytes = leftLength,
                LockMemoryPolicy = LockMemoryPolicy.None,
            };
            byte[] hash = Blake2B.ComputeHash(arrayBuffer, leftLength, rightLength, cfg, null!);
            Xor(arrayBuffer.AsSpan(0, leftLength), hash);
        }

        static byte[] PersonalizeH(byte i) => new byte[] { 85, 65, 95, 70, 52, 74, 117, 109, 98, 108, 101, 95, 72, i, 0, 0 };

        static byte[] PersonalizeG(byte i, ushort j) => new byte[] { 85, 65, 95, 70, 52, 74, 117, 109, 98, 108, 101, 95, 71, i, (byte)(j & 0xff), (byte)(j >> 8) };

        static int CeilDiv(int number, int divisor) => (number + divisor - 1) / divisor;

        static void Xor(Span<byte> left, ReadOnlySpan<byte> right)
        {
            for (int i = 0; i < left.Length & i < right.Length; i++)
            {
                left[i] ^= right[i];
            }
        }
    }

    /// <inheritdoc/>
    protected override bool CheckValidity(bool throwIfInvalid = false)
    {
        (int Tag, int Data)? length = Bech32.GetDecodedLength(this.Address);
        if (length is null)
        {
            return false;
        }

        Span<char> tag = stackalloc char[length.Value.Tag];
        Span<byte> data = stackalloc byte[length.Value.Data];
        return Bech32.Bech32m.TryDecode(this.Address, tag, data, out _, out _, out _);
    }

    private static ReadOnlyCollection<ZcashAddress> GetReceiversInPreferredOrder(IReadOnlyCollection<ZcashAddress> addresses)
    {
        // Although the UA encoding requires the receivers to be sorted in ascending Type Code order,
        // we want to list receivers in order of preference, which is the opposite.
        List<ZcashAddress> sortedAddresses = addresses.ToList();
        sortedAddresses.Sort((a, b) => -a.UnifiedAddressTypeCode.CompareTo(b.UnifiedAddressTypeCode));
        return new(sortedAddresses);
    }
}
