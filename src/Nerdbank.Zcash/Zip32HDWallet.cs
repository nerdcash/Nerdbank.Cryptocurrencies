// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Security.Cryptography;
using Org.BouncyCastle.Math.EC;
using static Nerdbank.Cryptocurrencies.Bip32HDWallet;
using BCBigInteger = Org.BouncyCastle.Math.BigInteger;
using BCMath = Org.BouncyCastle.Math;

namespace Nerdbank.Zcash;

/// <summary>
/// Shielded Hierarchical Deterministic Wallets as defined in
/// <see href="https://zips.z.cash/zip-0032">ZIP-32</see>.
/// </summary>
public partial class Zip32HDWallet
{
	/// <summary>
	/// The coin type to use in the key derivation path.
	/// </summary>
	private const uint CoinType = 133;

	/// <summary>
	/// The value of the <c>purpose</c> position in the key path.
	/// </summary>
	private const uint Purpose = 32;

	/// <summary>
	/// The "Randomness Beacon".
	/// </summary>
	/// <remarks>
	/// The value for this is defined in <see href="https://zips.z.cash/protocol/protocol.pdf">the Zcash protocol</see> §5.9.
	/// </remarks>
	private static readonly BigInteger URS = BigInteger.Parse("096b36a5804bfacef1691e173c366a47ff5ba84a44f26ddd7e8d9f79d5b42df0", System.Globalization.NumberStyles.HexNumber);

	private static readonly BigInteger MaxDiversifierIndex = BigInteger.Pow(2, 88) - 1;

	private enum PrfExpandCodes : byte
	{
		SaplingAsk = 0x0,
		SaplingNsk = 0x1,
		SaplingOvk = 0x2,
		Esk = 0x4,
		Rcm = 0x5,
		OrchardAsk = 0x6,
		OrchardNk = 0x7,
		OrchardRivk = 0x8,
		Psi = 0x9,
		SaplingDk = 0x10,
		SaplingExtSK = 0x11,
		SaplingExtFVK = 0x12,
		SaplingAskDerive = 0x13,
		SaplingNskDerive = 0x14,
		SaplingOvkDerive = 0x15,
		SaplingDkDerive = 0x16,
		OrchardZip32Child = 0x81,
		OrchardDkOvk = 0x82,
		OrchardRivkInternal = 0x83,
	}

	/// <summary>
	/// Creates a key derivation path that conforms to the <see href="https://zips.z.cash/zip-0032#specification-wallet-usage">ZIP-32</see> specification
	/// of <c>m / purpose' / coin_type' / account'</c>.
	/// </summary>
	/// <inheritdoc cref="CreateKeyPath(uint, uint)"/>
	/// <remarks>
	/// With Zcash shielded accounts the recommended pattern is to have just one spending authority per account and issue
	/// unique receiving addresses via unique diversifiers to each party that may send ZEC to avoid correlation.
	/// This method creates a key path that will create the default key for the given account.
	/// </remarks>
	public static KeyPath CreateKeyPath(uint account)
	{
		// m / purpose' / coin_type' / account'
		return new(account | HardenedBit, new(CoinType | HardenedBit, new(Purpose | HardenedBit, KeyPath.Root)));
	}

	/// <summary>
	/// Creates a key derivation path that conforms to the <see href="https://zips.z.cash/zip-0032#specification-wallet-usage">ZIP-32</see> specification
	/// of <c>m / purpose' / coin_type' / account' / address_index</c>.
	/// </summary>
	/// <param name="account">
	/// <para>This level splits the key space into independent user identities, so the wallet never mixes the coins across different accounts.</para>
	/// <para>Users can use these accounts to organize the funds in the same fashion as bank accounts; for donation purposes (where all addresses are considered public), for saving purposes, for common expenses etc.</para>
	/// <para>Accounts are numbered from index 0 in sequentially increasing manner. This number is used as child index in BIP32 derivation.</para>
	/// <para>Hardened derivation is used at this level. The <see cref="HardenedBit"/> is added automatically if necessary.</para>
	/// <para>Software should prevent a creation of an account if a previous account does not have a transaction history (meaning none of its addresses have been used before).</para>
	/// <para>Software needs to discover all used accounts after importing the seed from an external source. Such an algorithm is described in "Account discovery" chapter.</para>
	/// </param>
	/// <param name="addressIndex">
	/// <para>The address index. Increment this to get a new receiving address that belongs to the same logical account.</para>
	/// <para>Addresses are numbered from index 0 in sequentially increasing manner. This number is used as child index in BIP32 derivation.</para>
	/// <para>This number should <em>not</em> include the <see cref="HardenedBit"/>.</para>
	/// </param>
	/// <returns>The key derivation path.</returns>
	/// <remarks>
	/// <para>zcashd 4.6.0 and later use <paramref name="account"/> <c>0x7fffffff</c> and hardened values for <paramref name="addressIndex"/>
	/// to generate "legacy" Sapling addresses.</para>
	/// <para>Ordinary Zcash accounts with shielded addresses should typically not use this overload
	/// because multiple spending authorities within the same account increases sync time and complexity
	/// where diversifiers typically suffice.
	/// Instead, the <see cref="CreateKeyPath(uint)"/> overload should be used.</para>
	/// </remarks>
	public static KeyPath CreateKeyPath(uint account, uint addressIndex)
	{
		// m / purpose' / coin_type' / account' / address_index
		return new(addressIndex, CreateKeyPath(account));
	}

	/// <summary>
	/// Encodes a <see cref="BigInteger"/> as a byte sequence in little-endian order.
	/// </summary>
	/// <param name="value">The integer.</param>
	/// <param name="output">
	/// A buffer to fill with the encoded integer.
	/// Any excess bytes will be 0-padded.
	/// </param>
	/// <returns>The number of bytes written to <paramref name="output"/>. Always its length.</returns>
	/// <remarks>This is the inverse operation to <see cref="LEOS2IP(ReadOnlySpan{byte})"/>.</remarks>
	/// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="output"/> is not large enough to store <paramref name="value"/>.</exception>
	private static int I2LEOSP(BigInteger value, Span<byte> output)
	{
		if (!value.TryWriteBytes(output, out int bytesWritten, isUnsigned: true))
		{
			throw new ArgumentException("Insufficient length", nameof(output));
		}

		return bytesWritten;
	}

	/// <summary>
	/// Decodes a <see cref="BigInteger"/> that has been encoded as a byte array in little-endian order.
	/// </summary>
	/// <param name="input">A little-endian ordered encoding of an integer.</param>
	/// <remarks>This is the inverse operation to <see cref="I2LEOSP(BigInteger, Span{byte})"/>.</remarks>
	private static BigInteger LEOS2IP(ReadOnlySpan<byte> input) => new(input, isUnsigned: true);

	/// <summary>
	/// Encodes a <see cref="BigInteger"/> as a bit sequence in little-endian order.
	/// </summary>
	/// <param name="value">The integer.</param>
	/// <param name="output">
	/// A buffer to fill with the encoded integer.
	/// Any excess bytes will be 0-padded.
	/// </param>
	/// <returns>The number of bytes written to <paramref name="output"/>. Always its length.</returns>
	private static int I2LEBSP(BigInteger value, Span<byte> output)
	{
		BigInteger bitSize = 2;
		output.Clear();
		int bitPosition = 0;
		for (; value > 0; bitPosition++)
		{
			(value, BigInteger remainder) = BigInteger.DivRem(value, bitSize);
			output[bitPosition / 8] |= (byte)(remainder << (bitPosition % 8));
		}

		return output.Length;
	}

	/// <summary>
	/// Reverse the order of bits in each individual byte in a buffer.
	/// </summary>
	/// <param name="input">The byte sequence to convert. Each byte's individual bits are assumed to be in MSB to LSB order.</param>
	/// <param name="output">Receives the converted byte sequence, where each byte's bits are reversed so they are in LSB to MSB order.</param>
	/// <returns>The number of bytes written to <paramref name="output"/> (i.e. the length of <paramref name="input"/>.)</returns>
	/// <remarks>
	/// Convert each group of 8 bits into a byte value with the least significant bit first, and concatenate the resulting bytes in the same order as the groups.
	/// </remarks>
	private static int LEBS2OSP(ReadOnlySpan<byte> input, Span<byte> output)
	{
		for (int i = 0; i < input.Length; i++)
		{
			byte originalValue = input[i];
			output[i] = (byte)(
				(originalValue & 0x80 >> 7) |
				(originalValue & 0x40 >> 5) |
				(originalValue & 0x20 >> 3) |
				(originalValue & 0x10 >> 1) |
				(originalValue & 0x08 << 1) |
				(originalValue & 0x04 << 3) |
				(originalValue & 0x02 << 5) |
				(originalValue & 0x01 << 7));
		}

		return input.Length;
	}

	/// <remarks>See <see href="https://forum.zcashcommunity.com/t/what-is-the-lebs2os-function-in-the-zip-32-spec/44886/2?u=aarnott">this comment</see>.</remarks>
	[Obsolete("The spec's reference to this function is a typo. Use LEBS2OSP instead.", error: true)]
	private static int LEBS2OS(ReadOnlySpan<byte> input, Span<byte> output)
	{
		throw new NotImplementedException();
	}

	/// <summary>
	/// Applies a Blake2b_512 hash to the concatenation of a pair of buffers.
	/// </summary>
	/// <param name="sk">The first input buffer.</param>
	/// <param name="domainSpecifier">The byte that is unique for the caller's purpose.</param>
	/// <param name="t">The second input buffer.</param>
	/// <param name="output">The buffer to receive the hash. Must be at least 64 bytes in length.</param>
	/// <returns>The number of bytes written to <paramref name="output"/>. Always 64.</returns>
	private static int PRFexpand(ReadOnlySpan<byte> sk, PrfExpandCodes domainSpecifier, ReadOnlySpan<byte> t, Span<byte> output)
	{
		Requires.Argument(output.Length >= 64, nameof(output), Strings.FormatUnexpectedLength(64, output.Length));

		// Rather than copy the input data into a single buffer, we could use an instance of Blake2B and call Update on it once for each input buffer.
		Span<byte> buffer = stackalloc byte[sk.Length + 1 + t.Length];
		sk.CopyTo(buffer);
		buffer[sk.Length] = (byte)domainSpecifier;
		t.CopyTo(buffer[(sk.Length + 1)..]);
		return Blake2B.ComputeHash(buffer, output, new Blake2B.Config { Personalization = "Zcash_ExpandSeed"u8, OutputSizeInBytes = 512 / 8 });
	}

	/// <inheritdoc cref="PRFexpand(ReadOnlySpan{byte}, PrfExpandCodes, ReadOnlySpan{byte}, Span{byte})"/>
	private static int PRFexpand(ReadOnlySpan<byte> sk, PrfExpandCodes domainSpecifier, Span<byte> output) => PRFexpand(sk, domainSpecifier, default, output);
}
