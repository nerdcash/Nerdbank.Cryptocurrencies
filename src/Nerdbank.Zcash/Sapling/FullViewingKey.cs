// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A viewing key that can decrypt incoming and outgoing transactions.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class FullViewingKey : IZcashKey, IEquatable<FullViewingKey>, IKeyWithTextEncoding
{
	private const string Bech32MainNetworkHRP = "zviews";
	private const string Bech32TestNetworkHRP = "zviewtestsapling";
	private readonly SubgroupPoint ak;
	private readonly NullifierDerivingKey nk;
	private readonly OutgoingViewingKey ovk;
	private string? textEncoding;

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
	/// </summary>
	/// <param name="ak">The Ak subgroup point.</param>
	/// <param name="nk">The nullifier deriving key.</param>
	/// <param name="viewingKey">The incoming viewing key.</param>
	/// <param name="ovk">The outgoing viewing key.</param>
	internal FullViewingKey(in SubgroupPoint ak, in NullifierDerivingKey nk, IncomingViewingKey viewingKey, in OutgoingViewingKey ovk)
	{
		this.ak = ak;
		this.nk = nk;
		this.IncomingViewingKey = viewingKey;
		this.ovk = ovk;
	}

	/// <summary>
	/// Gets the network this key should be used with.
	/// </summary>
	public ZcashNetwork Network => this.IncomingViewingKey.Network;

	/// <summary>
	/// Gets the Bech32 encoding of the full viewing key.
	/// </summary>
	public string TextEncoding
	{
		get
		{
			if (this.textEncoding is null)
			{
				Span<byte> encodedBytes = stackalloc byte[96];
				Span<char> encodedChars = stackalloc char[512];
				int byteLength = this.Encode(encodedBytes);
				string hrp = this.Network switch
				{
					ZcashNetwork.MainNet => Bech32MainNetworkHRP,
					ZcashNetwork.TestNet => Bech32TestNetworkHRP,
					_ => throw new NotSupportedException(),
				};
				int charLength = Bech32.Original.Encode(hrp, encodedBytes[..byteLength], encodedChars);
				this.textEncoding = new string(encodedChars[..charLength]);
			}

			return this.textEncoding;
		}
	}

	/// <summary>
	/// Gets or sets the incoming viewing key.
	/// </summary>
	public IncomingViewingKey IncomingViewingKey { get; protected set; }

	/// <summary>
	/// Gets the Ak subgroup point.
	/// </summary>
	internal ref readonly SubgroupPoint Ak => ref this.ak;

	/// <summary>
	/// Gets the nullifier deriving key.
	/// </summary>
	internal ref readonly NullifierDerivingKey Nk => ref this.nk;

	/// <summary>
	/// Gets the outgoing viewing key.
	/// </summary>
	internal ref readonly OutgoingViewingKey Ovk => ref this.ovk;

	private string DebuggerDisplay => this.TextEncoding;

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

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class
	/// from the bech32 encoding of an full viewing key as specified in ZIP-32.
	/// </summary>
	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	public static bool TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out FullViewingKey? key)
	{
		Span<char> hrp = stackalloc char[50];
		Span<byte> data = stackalloc byte[96];
		if (!Bech32.Original.TryDecode(encoding, hrp, data, out decodeError, out errorMessage, out (int TagLength, int DataLength) length))
		{
			key = null;
			return false;
		}

		hrp = hrp[..length.TagLength];
		ZcashNetwork? network = hrp switch
		{
			Bech32MainNetworkHRP => ZcashNetwork.MainNet,
			Bech32TestNetworkHRP => ZcashNetwork.TestNet,
			_ => null,
		};
		if (network is null)
		{
			decodeError = DecodeError.UnrecognizedHRP;
			errorMessage = $"Unexpected bech32 tag: {hrp}";
			key = null;
			return false;
		}

		key = Decode(data[..length.DataLength], network.Value);
		return true;
	}

	/// <inheritdoc/>
	public bool Equals(FullViewingKey? other)
	{
		return other is not null
			&& this.Ak.Equals(other.Ak)
			&& this.Nk.Equals(other.Nk)
			&& this.IncomingViewingKey.Equals(other.IncomingViewingKey)
			&& this.Ovk.Equals(other.Ovk);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is FullViewingKey other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode result = default;
		result.Add(this.Network);
		result.AddBytes(this.Ak);
		result.AddBytes(this.Nk);
		result.AddBytes(this.Ovk);
		result.Add(this.IncomingViewingKey);
		return result.ToHashCode();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class
	/// from expanded spending key data.
	/// </summary>
	/// <param name="ask">The ask value.</param>
	/// <param name="nsk">The nsk value.</param>
	/// <param name="ovk">The outgoing viewing key.</param>
	/// <param name="network">The network this key should be used with.</param>
	/// <returns>The initialized <see cref="FullViewingKey"/>.</returns>
	internal static FullViewingKey Create(ReadOnlySpan<byte> ask, ReadOnlySpan<byte> nsk, ReadOnlySpan<byte> ovk, ZcashNetwork network)
	{
		Span<byte> sk_bytes = stackalloc byte[96];
		ask.CopyToWithLengthCheck(sk_bytes[..32]);
		nsk.CopyToWithLengthCheck(sk_bytes[32..64]);
		ovk.CopyToWithLengthCheck(sk_bytes[64..96]);

		Span<byte> fvk_bytes = stackalloc byte[96];
		if (NativeMethods.TryGetSaplingFullViewingKeyFromExpandedSpendingKey(sk_bytes, fvk_bytes) != 0)
		{
			throw new ArgumentException();
		}

		ReadOnlySpan<byte> ak = fvk_bytes[..32];
		ReadOnlySpan<byte> nk = fvk_bytes[32..64];
		return new FullViewingKey(
			new(ak),
			new(nk),
			IncomingViewingKey.FromFullViewingKey(fvk_bytes, network),
			new(ovk));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class
	/// by deserializing it from a buffer.
	/// </summary>
	/// <param name="buffer">The 96-byte buffer to read from.</param>
	/// <param name="network">The network this key should be used with.</param>
	/// <returns>The deserialized key.</returns>
	internal static FullViewingKey Decode(ReadOnlySpan<byte> buffer, ZcashNetwork network)
	{
		ReadOnlySpan<byte> ak = buffer[0..32];
		ReadOnlySpan<byte> nk = buffer[32..64];
		ReadOnlySpan<byte> ovk = buffer[64..96];

		return new FullViewingKey(
			new SubgroupPoint(ak),
			new NullifierDerivingKey(nk),
			IncomingViewingKey.FromFullViewingKey(buffer, network),
			new OutgoingViewingKey(ovk));
	}

	/// <summary>
	/// Gets the raw encoding.
	/// </summary>
	/// <returns>The raw encoding.</returns>
	/// <remarks>
	/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec section 5.6.3.3</see>.
	/// </remarks>
	internal Bytes96 ToBytes()
	{
		Bytes96 result = default;
		this.Encode(result);
		return new(result);
	}

	/// <summary>
	/// Gets the raw encoding.
	/// </summary>
	/// <param name="rawEncoding">Receives the raw encoding. Must be at least 96 bytes in length.</param>
	/// <returns>The number of bytes written to <paramref name="rawEncoding"/>. Always 96.</returns>
	/// <remarks>
	/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec section 5.6.3.3</see>.
	/// </remarks>
	protected int Encode(Span<byte> rawEncoding)
	{
		int written = 0;
		written += this.Ak[..].CopyToRetLength(rawEncoding[written..]);
		written += this.Nk[..].CopyToRetLength(rawEncoding[written..]);
		written += this.Ovk[..].CopyToRetLength(rawEncoding[written..]);
		return written;
	}
}
