// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// The incoming viewing key for the Orchard pool.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class IncomingViewingKey : IUnifiedEncodingElement, IIncomingViewingKey, IEquatable<IncomingViewingKey>, IKeyWithTextEncoding
{
	private readonly RawEncodingBuffer rawEncoding;

	private string? textEncoding;

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class.
	/// </summary>
	/// <param name="rawEncoding">The raw encoding of the IVK (dk + ivk).</param>
	/// <param name="network">The network the key should be used on.</param>
	private IncomingViewingKey(ReadOnlySpan<byte> rawEncoding, ZcashNetwork network)
	{
		this.Network = network;
		this.rawEncoding = new(rawEncoding);
	}

	/// <summary>
	/// Gets the default address for this key.
	/// </summary>
	/// <remarks>
	/// Create additional diversified addresses using <see cref="CreateReceiver"/>.
	/// </remarks>
	/// <seealso cref="CreateDefaultReceiver"/>
	/// <seealso cref="CreateReceiver"/>
	public OrchardAddress DefaultAddress => new(this.CreateDefaultReceiver(), this.Network);

	/// <inheritdoc/>
	ZcashAddress IIncomingViewingKey.DefaultAddress => this.DefaultAddress;

	/// <inheritdoc/>
	public ZcashNetwork Network { get; }

	/// <inheritdoc/>
	byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.Orchard;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.UnifiedDataLength => this.Dk[..].Length + this.Ivk[..].Length;

	/// <inheritdoc/>
	public string TextEncoding => this.textEncoding ??= UnifiedViewingKey.Incoming.Create(this);

	/// <summary>
	/// Gets the diversifier key.
	/// </summary>
	internal ref readonly DiversifierKey Dk => ref DiversifierKey.From(this.rawEncoding[..32]);

	/// <summary>
	/// Gets the key agreement private key.
	/// </summary>
	internal ref readonly KeyAgreementPrivateKey Ivk => ref KeyAgreementPrivateKey.From(this.rawEncoding[32..]);

	/// <summary>
	/// Gets the raw encoding of this incoming viewing key.
	/// </summary>
	internal ReadOnlySpan<byte> RawEncoding => this.rawEncoding;

	private string DebuggerDisplay => this.DefaultAddress;

	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	static bool IKeyWithTextEncoding.TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
	{
		if (TryDecode(encoding, out decodeError, out errorMessage, out IncomingViewingKey? ivk))
		{
			key = ivk;
			return true;
		}

		key = null;
		return false;
	}

	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	public static bool TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IncomingViewingKey? key)
	{
		if (UnifiedViewingKey.TryDecode(encoding, out decodeError, out errorMessage, out UnifiedViewingKey? uvk))
		{
			key = uvk.GetViewingKey<IncomingViewingKey>();
			if (key is not null)
			{
				return true;
			}
			else
			{
				decodeError = DecodeError.TypeMismatch;
				errorMessage = Strings.ExpectedKeyNotContainedWithinUnifiedViewingKey;
				return false;
			}
		}
		else
		{
			key = null;
			return false;
		}
	}

	/// <summary>
	/// Creates an orchard receiver using this key and a given diversifier.
	/// </summary>
	/// <param name="diversifierIndex">An 11-byte deterministic diversifier.</param>
	/// <returns>The orchard receiver.</returns>
	public OrchardReceiver CreateReceiver(DiversifierIndex diversifierIndex)
	{
		Span<byte> rawReceiver = stackalloc byte[43];
		if (NativeMethods.TryGetOrchardRawPaymentAddress(this.rawEncoding, diversifierIndex, rawReceiver) != 0)
		{
			throw new InvalidKeyException(Strings.InvalidKey);
		}

		return new(rawReceiver);
	}

	/// <summary>
	/// Creates the default orchard receiver for this key.
	/// </summary>
	/// <returns>A receiver suitable for creating an address.</returns>
	/// <remarks>
	/// The default receiver is created using with a zero-filled diversifier.
	/// </remarks>
	public OrchardReceiver CreateDefaultReceiver() => this.CreateReceiver(default);

	/// <summary>
	/// Checks whether a given orchard receiver was derived from the same spending authority as this key
	/// (in other words: would ZEC sent to this receiver arrive in this account?).
	/// </summary>
	/// <param name="receiver">The receiver to test.</param>
	/// <returns><see langword="true"/> if this receiver would send ZEC to this account; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// <para>This is a simpler front-end for the <see cref="TryGetDiversifierIndex"/> method,
	/// which runs a similar test but also provides the decrypted diversifier index.</para>
	/// </remarks>
	public bool CheckReceiver(in OrchardReceiver receiver) => this.TryGetDiversifierIndex(receiver, out _);

	/// <summary>
	/// Checks whether a given orchard receiver was derived from the same spending authority as this key
	/// (in other words: would ZEC sent to this receiver arrive in this account?).
	/// If so, the diversifier that was used to create it is decrypted back into its original index.
	/// </summary>
	/// <param name="receiver">The receiver to decrypt.</param>
	/// <param name="diversifierIndex">Receives the original diversifier index, if successful.</param>
	/// <returns>A value indicating whether the receiver could be decrypted successfully (i.e. the receiver came from this key).</returns>
	/// <remarks>
	/// <para>Use <see cref="CheckReceiver(in OrchardReceiver)"/> for a simpler API if the diversifier index is not required.</para>
	/// </remarks>
	public bool TryGetDiversifierIndex(in OrchardReceiver receiver, [NotNullWhen(true)] out DiversifierIndex? diversifierIndex)
	{
		Span<byte> diversifierSpan = stackalloc byte[11];
		switch (NativeMethods.DecryptOrchardDiversifier(this.RawEncoding, receiver, diversifierSpan))
		{
			case 0:
				diversifierIndex = new(diversifierSpan);
				return true;
			case 1:
				diversifierIndex = null;
				return false;
			default: throw new ArgumentException();
		}
	}

	/// <inheritdoc/>
	public bool Equals(IncomingViewingKey? other)
	{
		return other is not null
			&& this.Network == other.Network
			&& this.rawEncoding.Equals(other.rawEncoding);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is IncomingViewingKey other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode result = default;
		result.Add(this.Network);
		result.AddBytes(this.rawEncoding);
		return result.ToHashCode();
	}

	/// <inheritdoc/>
	int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination) => this.Encode(destination);

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class
	/// from its raw encoding.
	/// </summary>
	/// <param name="buffer">The buffer to read from. 64-bytes will be read from this.</param>
	/// <param name="network">The network the key should be used on.</param>
	/// <returns>The incoming viewing key.</returns>
	internal static IncomingViewingKey Decode(ReadOnlySpan<byte> buffer, ZcashNetwork network) => new(buffer, network);

	/// <summary>
	/// Reads the viewing key from its representation in a unified viewing key.
	/// </summary>
	/// <param name="keyContribution">The data that would have been written by <see cref="IUnifiedEncodingElement.WriteUnifiedData(Span{byte})"/>.</param>
	/// <param name="network">The network the key should be used with.</param>
	/// <returns>The deserialized key.</returns>
	internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network) => Decode(keyContribution, network);

	/// <summary>
	/// Initializen a new instance of the <see cref="IncomingViewingKey" /> class
	/// from the raw encoding of a full viewing key.
	/// </summary>
	/// <param name="fvkRawEncoding">The raw encoding of the full viewing key.</param>
	/// <param name="network">The network the key should be used on.</param>
	/// <returns>The incoming viewing key.</returns>
	/// <exception cref="InvalidKeyException">Thrown when the full viewing key encoding is invalid.</exception>
	internal static IncomingViewingKey FromFullViewingKey(ReadOnlySpan<byte> fvkRawEncoding, ZcashNetwork network)
	{
		Span<byte> ivk = stackalloc byte[64];
		if (NativeMethods.GetOrchardIncomingViewingKeyFromFullViewingKey(fvkRawEncoding, ivk) != 0)
		{
			throw new InvalidKeyException();
		}

		return new IncomingViewingKey(ivk, network);
	}

	/// <summary>
	/// Writes out the raw encoding of the incoming viewing key.
	/// </summary>
	/// <param name="buffer">The buffer to write to. Must be at least 64 bytes.</param>
	/// <returns>The number of bytes written. Always 64.</returns>
	internal int Encode(Span<byte> buffer)
	{
		int written = 0;
		written += this.Dk[..].CopyToRetLength(buffer[written..]);
		written += this.Ivk[..].CopyToRetLength(buffer[written..]);
		return written;
	}

	[InlineArray(Length)]
	private struct RawEncodingBuffer : IEquatable<RawEncodingBuffer>
	{
		public const int Length = 64;

		private byte element;

		internal RawEncodingBuffer(ReadOnlySpan<byte> value)
		{
			value.CopyToWithLengthCheck(this);
		}

		/// <summary>
		/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
		/// </summary>
		/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
		/// <returns>The strongly-typed element.</returns>
		public static ref readonly RawEncodingBuffer From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, RawEncodingBuffer>(value));

		/// <inheritdoc/>
		readonly bool IEquatable<RawEncodingBuffer>.Equals(RawEncodingBuffer other) => this[..].SequenceEqual(other);

		/// <inheritdoc cref="IEquatable{T}.Equals"/>
		public readonly bool Equals(in RawEncodingBuffer other) => this[..].SequenceEqual(other);
	}
}
