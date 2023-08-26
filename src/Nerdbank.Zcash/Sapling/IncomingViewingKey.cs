// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A viewing key for incoming transactions.
/// </summary>
public class IncomingViewingKey : IUnifiedEncodingElement, IIncomingViewingKey, IEquatable<IncomingViewingKey>
{
	private const string Bech32MainNetworkHRP = "zivks";
	private const string Bech32TestNetworkHRP = "zivktestsapling";

	private readonly Bytes32 ivk;
	private readonly DiversifierKey? dk;

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class.
	/// </summary>
	/// <param name="ivk">The 32-byte ivk value.</param>
	/// <param name="dk">The 11-byte diversification key.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal IncomingViewingKey(ReadOnlySpan<byte> ivk, ReadOnlySpan<byte> dk, ZcashNetwork network)
	{
		this.ivk = new(ivk);
		this.dk = dk.Length > 0 ? new(dk) : null;
		this.Network = network;
	}

	/// <summary>
	/// Gets the network this key should be used with.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Gets the default address for this spending key.
	/// </summary>
	/// <remarks>
	/// Create additional diversified addresses using <see cref="TryCreateReceiver(ref BigInteger, out SaplingReceiver)"/>.
	/// </remarks>
	public SaplingAddress DefaultAddress => new(this.CreateDefaultReceiver(), this.Network);

	/// <inheritdoc/>
	string IIncomingViewingKey.DefaultAddress => this.DefaultAddress.ToString();

	/// <inheritdoc/>
	byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.Sapling;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.UnifiedDataLength => 32 * 2;

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
	internal ref readonly Bytes32 Ivk => ref this.ivk;

	/// <summary>
	/// Gets the diversification key.
	/// </summary>
	internal ref readonly DiversifierKey? Dk => ref this.dk;

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
	/// Creates a sapling receiver using this key and a given diversifier.
	/// </summary>
	/// <param name="index">
	/// The diversifier index to start searching at, in the range of 0..(2^88 - 1).
	/// Not every index will produce a valid diversifier. About half will fail.
	/// The default diversifier is defined as the smallest non-negative index that produces a valid diversifier.
	/// This value will be incremented until a diversifier can be found.
	/// </param>
	/// <param name="receiver">Receives the sapling receiver, if successful.</param>
	/// <returns>
	/// <see langword="true"/> if a valid diversifier could be produced at or above the initial value given by <paramref name="index"/>.
	/// <see langword="false"/> if no valid diversifier could be found at or above <paramref name="index"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is negative.</exception>
	public bool TryCreateReceiver(ref BigInteger index, out SaplingReceiver receiver)
	{
		Requires.Range(index >= 0, nameof(index));

		Span<byte> indexBytes = stackalloc byte[11];
		if (!index.TryWriteBytes(indexBytes, out _, isUnsigned: true))
		{
			throw new ArgumentException("Index must fit within 11 bytes.");
		}

		bool result = this.TryCreateReceiver(indexBytes, out receiver);

		if (result)
		{
			// The index may have been changed. Apply that change to our ref parameter.
			index = new BigInteger(indexBytes, isUnsigned: true);
		}

		return result;
	}

	/// <summary>
	/// Creates a sapling receiver using this key and a given diversifier.
	/// </summary>
	/// <param name="diversifierIndex">
	/// The diversifier index to start searching at, in the range of 0..(2^88 - 1).
	/// Not every index will produce a valid diversifier. About half will fail.
	/// The default diversifier is defined as the smallest non-negative index that produces a valid diversifier.
	/// This value will be incremented until a diversifier can be found, considering the buffer to be a little-endian encoded integer.
	/// </param>
	/// <param name="receiver">Receives the sapling receiver, if successful.</param>
	/// <returns>
	/// <see langword="true"/> if a valid diversifier could be produced at or above the initial value given by <paramref name="diversifierIndex"/>.
	/// <see langword="false"/> if no valid diversifier could be found at or above <paramref name="diversifierIndex"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="diversifierIndex"/> is negative.</exception>
	public bool TryCreateReceiver(Span<byte> diversifierIndex, out SaplingReceiver receiver)
	{
		Verify.Operation(this.Dk.HasValue, "This IVK was not created with a diversifier key.");
		Span<byte> receiverBytes = stackalloc byte[SaplingReceiver.Length];
		if (NativeMethods.TryGetSaplingReceiver(this.ivk.Value, this.Dk.Value.Value, diversifierIndex, receiverBytes) != 0)
		{
			return false;
		}

		receiver = new(receiverBytes);
		return true;
	}

	/// <summary>
	/// Creates the default sapling receiver for this key.
	/// </summary>
	/// <returns>The receiver.</returns>
	public SaplingReceiver CreateDefaultReceiver()
	{
		Span<byte> diversifier = stackalloc byte[11];
		Assumes.True(this.TryCreateReceiver(diversifier, out SaplingReceiver receiver));
		return receiver;
	}

	/// <summary>
	/// Checks whether a given sapling receiver was derived from the same spending authority as this key
	/// (in other words: would ZEC sent to this receiver arrive in this account?).
	/// </summary>
	/// <param name="receiver">The receiver to test.</param>
	/// <returns><see langword="true"/> if this receiver would send ZEC to this account; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// <para>This is a simpler front-end for the <see cref="TryGetDiversifierIndex(SaplingReceiver, Span{byte})"/> method,
	/// which runs a similar test but also provides the decrypted diversifier index.</para>
	/// </remarks>
	public bool CheckReceiver(SaplingReceiver receiver)
	{
		Span<byte> diversifier = stackalloc byte[11];
		return this.TryGetDiversifierIndex(receiver, diversifier);
	}

	/// <summary>
	/// Checks whether a given sapling receiver was derived from the same spending authority as this key
	/// (in other words: would ZEC sent to this receiver arrive in this account?).
	/// If so, the diversifier that was used to create it is decrypted back into its original index.
	/// </summary>
	/// <param name="receiver">The receiver to decrypt.</param>
	/// <param name="diversifierIndex">Receives the original diversifier index, if successful.</param>
	/// <returns>A value indicating whether the receiver could be decrypted successfully (i.e. the receiver came from this key).</returns>
	/// <remarks>
	/// <para>Use <see cref="CheckReceiver(SaplingReceiver)"/> for a simpler API if the diversifier index is not required.</para>
	/// </remarks>
	public bool TryGetDiversifierIndex(SaplingReceiver receiver, Span<byte> diversifierIndex)
	{
		Verify.Operation(this.Dk.HasValue, "This IVK was not created with a diversifier key.");

		return NativeMethods.DecryptSaplingDiversifierWithIvk(this.Ivk.Value, this.Dk.Value.Value, receiver.Span, diversifierIndex) switch
		{
			0 => true,
			1 => false,
			_ => throw new ArgumentException(),
		};
	}

	/// <inheritdoc cref="TryGetDiversifierIndex(SaplingReceiver, Span{byte})"/>
	public bool TryGetDiversifierIndex(SaplingReceiver receiver, [NotNullWhen(true)] out BigInteger? diversifierIndex)
	{
		Span<byte> diversifierIndexBytes = stackalloc byte[11];
		if (this.TryGetDiversifierIndex(receiver, diversifierIndexBytes))
		{
			diversifierIndex = new BigInteger(diversifierIndexBytes, isUnsigned: true);
			return true;
		}
		else
		{
			diversifierIndex = null;
			return false;
		}
	}

	/// <inheritdoc/>
	int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination)
	{
		Verify.Operation(this.Dk.HasValue, "Cannot write this IVK because its dk value is unknown.");
		int written = 0;
		written += this.Dk.Value.Value.CopyToRetLength(destination[written..]);
		written += this.Ivk.Value.CopyToRetLength(destination[written..]);
		return written;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class
	/// by deserializing it from a buffer.
	/// </summary>
	/// <param name="buffer">The 32-byte buffer to read from.</param>
	/// <param name="network">The network this key should be used with.</param>
	/// <returns>The deserialized key.</returns>
	internal static IncomingViewingKey Decode(ReadOnlySpan<byte> buffer, ZcashNetwork network) => new(buffer[..32], default, network);

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> class
	/// from elements of a full viewing key.
	/// </summary>
	/// <param name="ak">The Ak subgroup point.</param>
	/// <param name="nk">The nullifier deriving key.</param>
	/// <param name="dk">The diversification key. May be default. Required for inclusion in a unified viewing key.</param>
	/// <param name="network">The network on which this key should operate.</param>
	/// <returns>The constructed incoming viewing key.</returns>
	/// <exception cref="InvalidKeyException">Thrown if an error occurs while parsing the inputs.</exception>
	internal static IncomingViewingKey FromFullViewingKey(ReadOnlySpan<byte> ak, ReadOnlySpan<byte> nk, ReadOnlySpan<byte> dk, ZcashNetwork network)
	{
		Span<byte> ivk = stackalloc byte[32];
		if (NativeMethods.DeriveSaplingIncomingViewingKeyFromFullViewingKey(ak, nk, ivk) != 0)
		{
			throw new InvalidKeyException();
		}

		return new(ivk, dk, network);
	}

	/// <inheritdoc cref="Orchard.FullViewingKey.DecodeUnifiedViewingKeyContribution(ReadOnlySpan{byte}, ZcashNetwork)"/>
	internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network)
	{
		Requires.Argument(keyContribution.Length == 64, nameof(keyContribution), "Unexpected length.");
		ReadOnlySpan<byte> dk = keyContribution[0..32];
		ReadOnlySpan<byte> ivk = keyContribution[32..64];
		return new IncomingViewingKey(ivk, dk, network);
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
