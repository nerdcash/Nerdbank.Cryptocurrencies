// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A viewing key that can decrypt incoming and outgoing transactions.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class FullViewingKey : IFullViewingKey, IEquatable<FullViewingKey>
{
	private const string Bech32MainNetworkHRP = "zviews";
	private const string Bech32TestNetworkHRP = "zviewtestsapling";

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
	/// </summary>
	/// <param name="ak">The Ak subgroup point.</param>
	/// <param name="nk">The nullifier deriving key.</param>
	/// <param name="viewingKey">The incoming viewing key.</param>
	/// <param name="ovk">The outgoing viewing key.</param>
	internal FullViewingKey(SubgroupPoint ak, NullifierDerivingKey nk, IncomingViewingKey viewingKey, OutgoingViewingKey ovk)
	{
		this.Ak = ak;
		this.Nk = nk;
		this.IncomingViewingKey = viewingKey;
		this.Ovk = ovk;
	}

	/// <summary>
	/// Gets the network this key should be used with.
	/// </summary>
	public ZcashNetwork Network => this.IncomingViewingKey.Network;

	/// <summary>
	/// Gets the Bech32 encoding of the full viewing key.
	/// </summary>
	public string Encoded
	{
		get
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
			return new string(encodedChars[..charLength]);
		}
	}

	/// <summary>
	/// Gets or sets the incoming viewing key.
	/// </summary>
	public IncomingViewingKey IncomingViewingKey { get; protected set; }

	/// <inheritdoc/>
	IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

	/// <summary>
	/// Gets the Ak subgroup point.
	/// </summary>
	internal SubgroupPoint Ak { get; }

	/// <summary>
	/// Gets the nullifier deriving key.
	/// </summary>
	internal NullifierDerivingKey Nk { get; }

	/// <summary>
	/// Gets the outgoing viewing key.
	/// </summary>
	internal OutgoingViewingKey Ovk { get; }

	private string DebuggerDisplay => this.IncomingViewingKey.DefaultAddress.ToString();

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class
	/// from the bech32 encoding of an full viewing key as specified in ZIP-32.
	/// </summary>
	/// <param name="encoding">The bech32-encoded key.</param>
	/// <returns>An initialized <see cref="FullViewingKey"/>.</returns>
	/// <remarks>
	/// This method can parse the output of the <see cref="Encoded"/> property.
	/// </remarks>
	public static FullViewingKey FromEncoded(ReadOnlySpan<char> encoding)
	{
		Span<char> hrp = stackalloc char[50];
		Span<byte> data = stackalloc byte[96];
		(int tagLength, int dataLength) = Bech32.Original.Decode(encoding, hrp, data);
		hrp = hrp[..tagLength];
		ZcashNetwork network = hrp switch
		{
			Bech32MainNetworkHRP => ZcashNetwork.MainNet,
			Bech32TestNetworkHRP => ZcashNetwork.TestNet,
			_ => throw new InvalidKeyException($"Unexpected bech32 tag: {hrp}"),
		};
		return Decode(data[..dataLength], network);
	}

	/// <inheritdoc/>
	public bool Equals(FullViewingKey? other)
	{
		return other is not null
			&& this.Ak.Value.SequenceEqual(other.Ak.Value)
			&& this.Nk.Value.SequenceEqual(other.Nk.Value)
			&& this.IncomingViewingKey.Equals(other.IncomingViewingKey)
			&& this.Ovk.Value.SequenceEqual(other.Ovk.Value);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is FullViewingKey other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode result = default;
		result.Add(this.Network);
		result.AddBytes(this.Ak.Value);
		result.AddBytes(this.Nk.Value);
		result.AddBytes(this.Ovk.Value);
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
			IncomingViewingKey.FromFullViewingKey(ak, nk, default, network),
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
			IncomingViewingKey.FromFullViewingKey(ak, nk, dk: default, network),
			new OutgoingViewingKey(ovk));
	}

	/// <summary>
	/// Gets the raw encoding.
	/// </summary>
	/// <param name="rawEncoding">Receives the raw encoding. Must be at least 96 bytes in length.</param>
	/// <returns>The number of bytes written to <paramref name="rawEncoding"/>. Always 96.</returns>
	/// <remarks>
	/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec section 5.6.3.3</see>.
	/// </remarks>
	internal int Encode(Span<byte> rawEncoding)
	{
		int written = 0;
		written += this.Ak.Value.CopyToRetLength(rawEncoding[written..]);
		written += this.Nk.Value.CopyToRetLength(rawEncoding[written..]);
		written += this.Ovk.Value.CopyToRetLength(rawEncoding[written..]);
		return written;
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
		Span<byte> result = stackalloc byte[96];
		this.Encode(result);
		return new(result);
	}
}
