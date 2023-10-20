// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// A viewing key that can decrypt incoming and outgoing transactions.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class FullViewingKey : IUnifiedEncodingElement, IFullViewingKey, IEquatable<FullViewingKey>, IKeyWithTextEncoding
{
	private readonly Bytes96 rawEncoding;
	private string? textEncoding;

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
	/// </summary>
	/// <param name="rawEncoding">The 96-byte raw encoding of the full viewing key.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal FullViewingKey(ReadOnlySpan<byte> rawEncoding, ZcashNetwork network)
	{
		this.rawEncoding = new(rawEncoding);
		this.IncomingViewingKey = IncomingViewingKey.FromFullViewingKey(rawEncoding, network);
	}

	/// <summary>
	/// Gets the network this key should be used with.
	/// </summary>
	public ZcashNetwork Network => this.IncomingViewingKey.Network;

	/// <summary>
	/// Gets the incoming viewing key.
	/// </summary>
	public IncomingViewingKey IncomingViewingKey { get; }

	/// <inheritdoc/>
	IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

	/// <inheritdoc/>
	byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.Orchard;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.UnifiedDataLength => 32 * 3;

	/// <inheritdoc/>
	public string TextEncoding => this.textEncoding ??= UnifiedViewingKey.Full.Create(this);

	/// <summary>
	/// Gets the spend validating key.
	/// </summary>
	internal SpendValidatingKey Ak => new(this.rawEncoding.Value[0..32]);

	/// <summary>
	/// Gets the nullifier deriving key.
	/// </summary>
	internal NullifierDerivingKey Nk => new(this.rawEncoding.Value[32..64]);

	/// <summary>
	/// Gets the commit randomness.
	/// </summary>
	internal CommitIvkRandomness Rivk => new(this.rawEncoding.Value[64..]);

	/// <summary>
	/// Gets the 96-byte raw encoding of this key.
	/// </summary>
	internal ReadOnlySpan<byte> RawEncoding => this.rawEncoding.Value;

	private string DebuggerDisplay => this.IncomingViewingKey.DefaultAddress.ToString();

	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	static bool IKeyWithTextEncoding.TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
	{
		if (TryDecode(encoding, out decodeError, out errorMessage, out FullViewingKey? fvk))
		{
			key = fvk;
			return true;
		}

		key = null;
		return false;
	}

	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	public static bool TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out FullViewingKey? key)
	{
		if (UnifiedViewingKey.TryDecode(encoding, out decodeError, out errorMessage, out UnifiedViewingKey? uvk))
		{
			key = uvk.GetViewingKey<FullViewingKey>();
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
	/// Derives the internal full viewing key from this.
	/// </summary>
	/// <returns>The internal viewing key.</returns>
	/// <remarks>
	/// This method assumes that <em>this</em> viewing key is the public facing one.
	/// The caller should take care to not call this method on what is already the internal key.
	/// </remarks>
	public FullViewingKey DeriveInternal()
	{
		Span<byte> internalFvk = stackalloc byte[(32 * 3) + 32];
		this.Ak.Value.CopyTo(internalFvk[0..32]);
		this.Nk.Value.CopyTo(internalFvk[32..64]);
		ZcashUtilities.PRFexpand(this.Rivk.Value, PrfExpandCodes.OrchardRivkInternal, internalFvk[..64], internalFvk[64..]);
		int result = NativeMethods.OrchardToScalarToRepr(internalFvk[64..], internalFvk[64..96]);
		if (result != 0)
		{
			throw new InvalidKeyException($"Result code {result}.");
		}

		return new(internalFvk[..96], this.Network);
	}

	/// <inheritdoc/>
	public bool Equals(FullViewingKey? other)
	{
		return other is not null
			&& this.rawEncoding.Value.SequenceEqual(other.rawEncoding.Value)
			&& this.Network == other.Network;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is FullViewingKey other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode result = default;
		result.Add(this.Network);
		result.AddBytes(this.rawEncoding.Value);
		return result.ToHashCode();
	}

	/// <inheritdoc/>
	int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination)
	{
		int written = 0;
		written += this.rawEncoding.Value.CopyToRetLength(destination[written..]);
		return written;
	}

	/// <summary>
	/// Reads the viewing key from its representation in a unified viewing key.
	/// </summary>
	/// <param name="keyContribution">The data that would have been written by <see cref="IUnifiedEncodingElement.WriteUnifiedData(Span{byte})"/>.</param>
	/// <param name="network">The network the key should be used with.</param>
	/// <returns>The deserialized key.</returns>
	internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network)
	{
		return new FullViewingKey(keyContribution, network);
	}
}
