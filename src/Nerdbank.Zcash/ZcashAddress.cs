// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A Zcash address.
/// </summary>
public abstract class ZcashAddress : IEquatable<ZcashAddress>, IUnifiedEncodingElement
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashAddress"/> class.
	/// </summary>
	/// <param name="address">The address in string form.</param>
	protected ZcashAddress(string address)
	{
		Requires.NotNullOrEmpty(address);
		this.Address = address.ToString();
	}

	/// <summary>
	/// Enumerates the kinds of matches that may occur between two Zcash addresses.
	/// </summary>
	/// <remarks>
	/// In the documentation for each enumerated value, the "test address" is the address passed as an argument to to <see cref="IsMatch(ZcashAddress)"/>
	/// while the "receiving address" is the address on which <see cref="IsMatch(ZcashAddress)"/> is called.
	/// </remarks>
	[Flags]
	public enum Match
	{
		/// <summary>
		/// The two addresses have no receiver types in common. The addresses do not match at all.
		/// </summary>
		NoMatchingReceiverTypes = 0x0,

		/// <summary>
		/// The two addresses have at least one receiver type in common whose values do not match.
		/// </summary>
		/// <remarks>
		/// If <see cref="MatchingReceiversFound"/> is not set, this is a total mismatch.
		/// If <see cref="MatchingReceiversFound"/> is also set, this is a partial match and <em>may</em> signify
		/// a contrived address that is meant to fool someone into sending funds to the wrong person.
		/// </remarks>
		MismatchingReceiversFound = unchecked((int)0x80000000),

		/// <summary>
		/// The two addresses have at least one receiver type in common whose values match.
		/// </summary>
		MatchingReceiversFound = 0x1,

		/// <summary>
		/// The test address contains receiver types not found in the receiving address.
		/// </summary>
		UniqueReceiverTypesInTestAddress = 0x2,

		/// <summary>
		/// The receiving address contains receiver types not found in the test address.
		/// </summary>
		UniqueReceiverTypesInReceivingAddress = 0x4,
	}

	/// <summary>
	/// Gets the network the address belongs to.
	/// </summary>
	/// <exception cref="InvalidAddressException">Thrown if the address is invalid.</exception>
	public abstract ZcashNetwork Network { get; }

	/// <summary>
	/// Gets the address as a string.
	/// </summary>
	public string Address { get; }

	/// <summary>
	/// Gets a value indicating whether funds sent to this address may be shielded.
	/// </summary>
	/// <remarks>
	/// This property being <see langword="true" /> may not guarantee funds will be shielded.
	/// For example if this is a unified address carrying both shielded and unshielded receivers,
	/// unshielded funds could be transmitted.
	/// </remarks>
	public abstract bool HasShieldedReceiver { get; }

	/// <summary>
	/// Gets the total length of this address's contribution to a unified address.
	/// </summary>
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	int IUnifiedEncodingElement.UnifiedDataLength => this.ReceiverEncodingLength;

	/// <summary>
	/// Gets the type code to use when embedded in a unified address.
	/// </summary>
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	byte IUnifiedEncodingElement.UnifiedTypeCode => this.UnifiedTypeCode;

	/// <inheritdoc cref="IUnifiedEncodingElement.UnifiedTypeCode"/>
	internal abstract byte UnifiedTypeCode { get; }

	/// <summary>
	/// Gets the length of the receiver encoding in a unified address.
	/// </summary>
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	internal abstract int ReceiverEncodingLength { get; }

	/// <summary>
	/// Implicitly casts this address to a string.
	/// </summary>
	/// <param name="address">The address to convert.</param>
	[return: NotNullIfNotNull(nameof(address))]
	public static implicit operator string?(ZcashAddress? address) => address?.Address;

	/// <summary>
	/// Parse a string of characters as an address.
	/// </summary>
	/// <param name="address">The address.</param>
	/// <returns>The parsed address.</returns>
	/// <exception type="InvalidAddressException">Thrown if the address is invalid.</exception>
	public static ZcashAddress Decode(string address)
	{
		return TryDecode(address, out _, out string? errorMessage, out ZcashAddress? result)
			? result
			: throw new InvalidAddressException(errorMessage);
	}

	/// <summary>
	/// Tries to parse a string of characters as an address.
	/// </summary>
	/// <param name="address">The address.</param>
	/// <param name="errorCode">Receives the error code if parsing fails.</param>
	/// <param name="errorMessage">Receives the error message if the parsing fails.</param>
	/// <param name="result">Receives the parsed address.</param>
	/// <returns>A value indicating whether the address parsed to a valid address.</returns>
	public static bool TryDecode(string address, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out ZcashAddress? result)
	{
		Requires.NotNull(address, nameof(address));

		for (int attempt = 0; ; attempt++)
		{
			switch (attempt)
			{
				case 0:
					if (TransparentAddress.TryParse(address, out errorCode, out errorMessage, out TransparentAddress? tAddr))
					{
						result = tAddr;
						return true;
					}

					break;
				case 2:
					if (SproutAddress.TryParse(address, out SproutAddress? sproutAddr, out errorCode, out errorMessage))
					{
						result = sproutAddr;
						return true;
					}

					break;
				case 1:
					if (SaplingAddress.TryParse(address, out errorCode, out errorMessage, out SaplingAddress? saplingAddr))
					{
						result = saplingAddr;
						return true;
					}

					break;
				case 3:
					if (UnifiedAddress.TryParse(address, out errorCode, out errorMessage, out UnifiedAddress? orchardAddr))
					{
						result = orchardAddr;
						return true;
					}

					break;
				default:
					result = null;
					errorCode = DecodeError.UnrecognizedAddressType;
					errorMessage = Strings.UnrecognizedAddress;
					return false;
			}

			// Any error other than an unrecognized address type is a fatal error.
			if (errorCode != DecodeError.UnrecognizedAddressType)
			{
				result = null;
				return false;
			}
		}
	}

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
	/// Checks for equivalence between this address and another.
	/// </summary>
	/// <param name="candidate">The "test address" to check for equivalence.</param>
	/// <returns>Receives the quality of the match.</returns>
	public Match IsMatch(ZcashAddress candidate)
	{
		Requires.NotNull(candidate);

		if (this.Equals(candidate))
		{
			return Match.MatchingReceiversFound;
		}

		// Go through each receiver type and see if there is a match.
		Match match = 0;

		// This must be manually maintained as new receiver types are added.
		TestReceiver<TransparentP2PKHReceiver>();
		TestReceiver<TransparentP2SHReceiver>();
		TestReceiver<TexReceiver>();
		TestReceiver<SproutReceiver>();
		TestReceiver<SaplingReceiver>();
		TestReceiver<OrchardReceiver>();

		return match;

		void TestReceiver<T>()
			where T : unmanaged, IPoolReceiver, IEquatable<T>
		{
			T? thisReceiver = this.GetPoolReceiver<T>();
			T? candidateReceiver = candidate.GetPoolReceiver<T>();
			switch ((thisReceiver, candidateReceiver))
			{
				case (null, null):
					break;
				case (null, not null):
					match |= Match.UniqueReceiverTypesInTestAddress;
					break;
				case (not null, null):
					match |= Match.UniqueReceiverTypesInReceivingAddress;
					break;
				case (not null, not null):
					if (thisReceiver.Value.Equals(candidateReceiver.Value))
					{
						match |= Match.MatchingReceiversFound;
					}
					else
					{
						match |= Match.MismatchingReceiversFound;
					}

					break;
			}
		}
	}

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
	/// Writes this address's contribution to a unified address.
	/// </summary>
	/// <param name="destination">The buffer to receive the UA contribution.</param>
	/// <returns>The number of bytes actually written to the buffer.</returns>
	int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination) => this.GetReceiverEncoding(destination);

	/// <summary>
	/// Writes out the encoded receiver for this address.
	/// </summary>
	/// <param name="output">The buffer to receive the encoded receiver.</param>
	/// <returns>The number of bytes written to <paramref name="output"/>.</returns>
	internal abstract int GetReceiverEncoding(Span<byte> output);

	/// <summary>
	/// Gets the length of the buffer required to call <see cref="WriteUAContribution{TReceiver}(in TReceiver, Span{byte})"/>.
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
	private protected static unsafe int WriteUAContribution<TReceiver>(in TReceiver receiver, Span<byte> destination)
		where TReceiver : unmanaged, IUnifiedPoolReceiver
	{
		int bytesWritten = 0;
		destination[bytesWritten++] = TReceiver.UnifiedReceiverTypeCode;
		bytesWritten += CompactSize.Encode((ulong)receiver.EncodingLength, destination[bytesWritten..]);
		receiver.Encode(destination[bytesWritten..]);
		bytesWritten += receiver.EncodingLength;
		return bytesWritten;
	}

	/// <summary>
	/// Casts one receiver type to another if they are compatible, returning <see langword="null" /> if the cast is invalid.
	/// </summary>
	/// <typeparam name="TNative">The native receiver type for the calling address.</typeparam>
	/// <typeparam name="TTarget">The generic type parameter provided to the caller, to which the receiver must be cast.</typeparam>
	/// <param name="receiver">The receiver to be cast.</param>
	/// <returns>The re-cast receiver, or <see langword="null" /> if the types do not match.</returns>
	private protected static TTarget? AsReceiver<TNative, TTarget>(in TNative receiver)
		where TNative : unmanaged, IPoolReceiver
		where TTarget : unmanaged, IPoolReceiver
	{
		return typeof(TNative) == typeof(TTarget) ? Unsafe.As<TNative, TTarget>(ref Unsafe.AsRef(in receiver)) : null;
	}
}
