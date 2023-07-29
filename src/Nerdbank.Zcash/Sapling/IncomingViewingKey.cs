// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A viewing key for incoming transactions.
/// </summary>
public class IncomingViewingKey : IKey, IEquatable<IncomingViewingKey>
{
	private const string Bech32MainNetworkHRP = "zivks";
	private const string Bech32TestNetworkHRP = "zivktestsapling";

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class.
	/// </summary>
	/// <param name="ivk">The 32-byte ivk value.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal IncomingViewingKey(ReadOnlySpan<byte> ivk, ZcashNetwork network)
	{
		this.Ivk = new(ivk);
		this.Network = network;
	}

	/// <summary>
	/// Gets the network this key should be used with.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <inheritdoc/>
	bool IKey.IsTestNet => this.Network != ZcashNetwork.MainNet;

	/// <summary>
	/// Gets the Bech32 encoding of the incoming viewing key.
	/// </summary>
	public string Encoded
	{
		get
		{
			Span<byte> encodedBytes = stackalloc byte[32];
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
	/// Gets the ivk value.
	/// </summary>
	internal Bytes32 Ivk { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class
	/// from the bech32 encoding of an incoming viewing key as specified in ZIP-32.
	/// </summary>
	/// <param name="encoding">The bech32-encoded key.</param>
	/// <returns>An initialized <see cref="IncomingViewingKey"/>.</returns>
	/// <remarks>
	/// This method can parse the output of the <see cref="Encoded"/> property.
	/// </remarks>
	public static IncomingViewingKey FromEncoded(ReadOnlySpan<char> encoding)
	{
		Span<char> hrp = stackalloc char[50];
		Span<byte> data = stackalloc byte[32];
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
	public bool Equals(IncomingViewingKey? other)
	{
		return other is not null
			&& this.Ivk.Value.SequenceEqual(other.Ivk.Value)
			&& this.Network == other.Network;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class
	/// by deserializing it from a buffer.
	/// </summary>
	/// <param name="buffer">The 32-byte buffer to read from.</param>
	/// <param name="network">The network this key should be used with.</param>
	/// <returns>The deserialized key.</returns>
	internal static IncomingViewingKey Decode(ReadOnlySpan<byte> buffer, ZcashNetwork network) => new(buffer[..32], network);

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class
	/// from elements of a full viewing key.
	/// </summary>
	/// <param name="ak">The Ak subgroup point.</param>
	/// <param name="nk">The nullifier deriving key.</param>
	/// <param name="network">The network on which this key should operate.</param>
	/// <returns>The constructed incoming viewing key.</returns>
	/// <exception cref="InvalidKeyException">Thrown if an error occurs while parsing the inputs.</exception>
	internal static IncomingViewingKey FromFullViewingKey(ReadOnlySpan<byte> ak, ReadOnlySpan<byte> nk, ZcashNetwork network)
	{
		Span<byte> ivk = stackalloc byte[32];
		if (NativeMethods.DeriveSaplingIncomingViewingKeyFromFullViewingKey(ak, nk, ivk) != 0)
		{
			throw new InvalidKeyException();
		}

		return new(ivk, network);
	}

	/// <summary>
	/// Gets the raw encoding.
	/// </summary>
	/// <param name="rawEncoding">Receives the raw encoding. Must be at least 32 bytes in length.</param>
	/// <returns>The number of bytes written to <paramref name="rawEncoding"/>. Always 32.</returns>
	/// <remarks>
	/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec sections 5.6.3.2 and 4.2.2</see>.
	/// </remarks>
	internal int Encode(Span<byte> rawEncoding)
	{
		int written = 0;
		written += this.Ivk.Value.CopyToRetLength(rawEncoding[written..]);
		return written;
	}
}
