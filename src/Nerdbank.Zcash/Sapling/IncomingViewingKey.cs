// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A viewing key for incoming transactions.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class IncomingViewingKey : IZcashKey, IEquatable<IncomingViewingKey>, IKeyWithTextEncoding
{
	private const string Bech32MainNetworkHRP = "zivks";
	private const string Bech32TestNetworkHRP = "zivktestsapling";

	private readonly Bytes32 ivk;
	private string? textEncoding;

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class.
	/// </summary>
	/// <param name="ivk">The 32-byte ivk value.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal IncomingViewingKey(ReadOnlySpan<byte> ivk, ZcashNetwork network)
	{
		this.ivk = new(ivk);
		this.Network = network;
	}

	/// <summary>
	/// Gets the network this key should be used with.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Gets the Bech32 encoding of the incoming viewing key.
	/// </summary>
	public string TextEncoding
	{
		get
		{
			if (this.textEncoding is null)
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
				this.textEncoding = new string(encodedChars[..charLength]);
			}

			return this.textEncoding;
		}
	}

	/// <summary>
	/// Gets the ivk value.
	/// </summary>
	internal ref readonly Bytes32 Ivk => ref this.ivk;

	private string DebuggerDisplay => this.TextEncoding;

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

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class
	/// from the bech32 encoding of an incoming viewing key as specified in ZIP-32.
	/// </summary>
	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	public static bool TryDecode(ReadOnlySpan<char> encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IncomingViewingKey? key)
	{
		Span<char> hrp = stackalloc char[50];
		Span<byte> data = stackalloc byte[32];
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
	public bool Equals(IncomingViewingKey? other)
	{
		return other is not null
			&& this.Ivk.Equals(other.Ivk)
			&& this.Network == other.Network;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is IncomingViewingKey other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode result = default;
		result.Add(this.Network);
		result.AddBytes(this.Ivk);
		return result.ToHashCode();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class
	/// by deserializing it from a buffer.
	/// </summary>
	/// <param name="buffer">The 32-byte buffer to read from.</param>
	/// <param name="network">The network this key should be used with.</param>
	/// <returns>The deserialized key.</returns>
	internal static IncomingViewingKey Decode(ReadOnlySpan<byte> buffer, ZcashNetwork network) => new(buffer[..32], network: network);

	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifiableIncomingViewingKey"/> class
	/// from elements of a full viewing key.
	/// </summary>
	/// <param name="fvk">The encoded full viewing key (ak, nk, ovk).</param>
	/// <param name="network">The network on which this key should operate.</param>
	/// <returns>The constructed incoming viewing key.</returns>
	/// <exception cref="InvalidKeyException">Thrown if an error occurs while parsing the inputs.</exception>
	internal static IncomingViewingKey FromFullViewingKey(ReadOnlySpan<byte> fvk, ZcashNetwork network)
	{
		Span<byte> ivk = stackalloc byte[32];
		if (NativeMethods.DeriveSaplingIncomingViewingKeyFromFullViewingKey(fvk, ivk) != 0)
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
		written += this.Ivk[..].CopyToRetLength(rawEncoding[written..]);
		return written;
	}
}
