// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Nerdbank.Bitcoin;

/// <summary>
/// A BIP-39 implementation that can generate new seed phrases and seed binary keys
/// that can be used to generate deterministic wallets using BIP-0032 or similar methods.
/// </summary>
[DebuggerDisplay($"Mnemonic: {{{nameof(SeedPhrase)}}}")]
public partial class Bip39Mnemonic
{
	private static readonly Encoding BinarySeedEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	/// <summary>
	/// The inline arrays where we store data to avoid allocating another object.
	/// </summary>
	private readonly FixedArrays fixedArrays;

	/// <summary>
	/// Initializes a new instance of the <see cref="Bip39Mnemonic"/> class.
	/// </summary>
	/// <param name="entropy">The entropy buffer that must be represented by the seed phrase. The length in bytes must be a non-zero multiple of 4. This should <em>not</em> include the checksum.</param>
	/// <param name="password">An optional password that when mixed in with the seed phrase will produce a unique binary seed. Whitespace will <em>not</em> be trimmed.</param>
	/// <exception cref="ArgumentException">Throw if the length of <paramref name="entropy"/> is not a multiple of 4.</exception>
	/// <remarks>
	/// This constructor is useful for those who already have the entropy created from a cryptographically strong random number generator.
	/// To generate a new mnemonic, use the <see cref="Create(int, ReadOnlyMemory{char})"/> static method.
	/// </remarks>
	public Bip39Mnemonic(ReadOnlySpan<byte> entropy, ReadOnlyMemory<char> password = default)
	{
		Requires.Argument(entropy.Length % 4 == 0 && entropy.Length > 0, nameof(entropy), Strings.LengthMustBeNonEmptyAndDivisibleBy4);

		this.fixedArrays = new FixedArrays(entropy.Length);
		entropy.CopyTo(this.fixedArrays.EntropyWritable);

		this.SeedPhrase = CreateSeedPhrase(entropy, WordList.Default);
		this.Password = password;

		CreateBinarySeed(this.SeedPhrase, password.Span, this.fixedArrays.SeedWritable);
	}

	/// <summary>
	/// Gets the seed phrase.
	/// </summary>
	/// <remarks>
	/// This does <em>not</em> include the <see cref="Password"/>.
	/// </remarks>
	public string SeedPhrase { get; }

	/// <summary>
	/// Gets the entropy described by the mnemonic.
	/// </summary>
	public ReadOnlySpan<byte> Entropy => this.fixedArrays.Entropy;

	/// <summary>
	/// Gets the optional password that is added to the mnemonic when creating the binary seed.
	/// </summary>
	/// <remarks>
	/// Any password is a valid addition, and each unique password will produce a unique, deterministic binary seed.
	/// </remarks>
	public ReadOnlyMemory<char> Password { get; }

	/// <summary>
	/// Gets the binary seed that may be used to generate deterministic wallets.
	/// </summary>
	public ReadOnlySpan<byte> Seed => this.fixedArrays.Seed;

	/// <inheritdoc cref="Create(int, ReadOnlyMemory{char})"/>
	public static Bip39Mnemonic Create(int entropyLengthInBits) => Create(entropyLengthInBits, default(ReadOnlyMemory<char>));

	/// <inheritdoc cref="Create(int, ReadOnlyMemory{char})"/>
	public static Bip39Mnemonic Create(int entropyLengthInBits, string? password) => Create(entropyLengthInBits, password?.AsMemory() ?? default);

	/// <summary>
	/// Generates a seed phrase for a newly generated secret.
	/// </summary>
	/// <param name="entropyLengthInBits">
	/// The number of secret bits that must be represented by the seed phrase.
	/// Common values are 128 (which produces a 12 word phrase), and 256 (which produces a 24 word phrase).
	/// Must be a multiple of 32.
	/// </param>
	/// <param name="password">An optional password that when mixed in with the seed phrase will produce a unique binary seed. Whitespace will <em>not</em> be trimmed.</param>
	/// <returns>The seed phrase.</returns>
	/// <exception cref="ArgumentException">Throw if <paramref name="entropyLengthInBits"/> is not a multiple of 32.</exception>
	public static Bip39Mnemonic Create(int entropyLengthInBits, ReadOnlyMemory<char> password)
	{
		Requires.Argument(entropyLengthInBits % 32 == 0, nameof(entropyLengthInBits), Strings.MustBeNonZeroMultipleOf32);
		Span<byte> entropy = stackalloc byte[entropyLengthInBits / 8];
		RandomNumberGenerator.Fill(entropy);
		return new(entropy, password);
	}

	/// <inheritdoc cref="TryParse(ReadOnlySpan{char}, out Bip39Mnemonic?, out DecodeError?, out string?)"/>
	/// <returns>The mnemonic.</returns>
	/// <exception cref="FormatException">Thrown if parsing fails.</exception>
	public static Bip39Mnemonic Parse(ReadOnlySpan<char> seedPhrase)
	{
		if (!TryParse(seedPhrase, out Bip39Mnemonic? result, out DecodeError? _, out string? errorMessage))
		{
			throw new FormatException(errorMessage);
		}

		return result;
	}

	/// <inheritdoc cref="Parse(ReadOnlySpan{char}, ReadOnlyMemory{char})"/>
	public static Bip39Mnemonic Parse(ReadOnlySpan<char> seedPhrase, string? password) => Parse(seedPhrase, password?.AsMemory() ?? default);

	/// <inheritdoc cref="TryParse(ReadOnlySpan{char}, ReadOnlyMemory{char}, out Bip39Mnemonic?, out DecodeError?, out string?)"/>
	/// <returns>The mnemonic.</returns>
	/// <exception cref="FormatException">Thrown if parsing fails.</exception>
	public static Bip39Mnemonic Parse(ReadOnlySpan<char> seedPhrase, ReadOnlyMemory<char> password)
	{
		if (!TryParse(seedPhrase, password, out Bip39Mnemonic? result, out DecodeError? _, out string? errorMessage))
		{
			throw new FormatException(errorMessage);
		}

		return result;
	}

	/// <summary>
	/// Decodes a seed phrase to the entropy data it represents.
	/// </summary>
	/// <param name="seedPhrase">The seed phrase. This <em>may</em> include exactly one extra word that serves as a password. Extra whitespace is ignored.</param>
	/// <param name="mnemonic">Receives the mnemonic.</param>
	/// <param name="decodeError">Receives the error code if decoding fails.</param>
	/// <param name="errorMessage">Receives the error message if decoding fails.</param>
	/// <returns><see langword="true" /> if decoding succeeds; <see langword="false" /> otherwise.</returns>
	public static bool TryParse(ReadOnlySpan<char> seedPhrase, [NotNullWhen(true)] out Bip39Mnemonic? mnemonic, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage)
	{
		// Look for an extra word in the seed phrase. If it is present, interpret that as a password.
		ReadOnlyMemory<char> password = default;
		if (CountWords(seedPhrase) % 3 == 1)
		{
			// There is one too many words in the seed phrase for it to be valid.
			// Assume the last word is a password and move it from the seed phrase to the password parameter.
			seedPhrase = seedPhrase.Trim();
			for (int i = seedPhrase.Length - 1; i >= 0; i--)
			{
				if (char.IsWhiteSpace(seedPhrase[i]))
				{
					password = seedPhrase[(i + 1)..].ToString().AsMemory();
					seedPhrase = seedPhrase[..i];
					break;
				}
			}
		}

		return TryParse(seedPhrase, password, out mnemonic, out decodeError, out errorMessage);
	}

	/// <inheritdoc cref="TryParse(ReadOnlySpan{char}, ReadOnlyMemory{char}, out Bip39Mnemonic?, out DecodeError?, out string?)"/>
	public static bool TryParse(ReadOnlySpan<char> seedPhrase, string? password, [NotNullWhen(true)] out Bip39Mnemonic? mnemonic, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage)
		=> TryParse(seedPhrase, password?.AsMemory() ?? default, out mnemonic, out decodeError, out errorMessage);

	/// <summary>
	/// Decodes a seed phrase to the entropy data it represents.
	/// </summary>
	/// <param name="seedPhrase">The seed phrase.</param>
	/// <param name="password">An optional password. This may contain any character including spaces, although spaces are not recommended because some wallet software does not provide a special password entry but instead accepts it as a <em>single</em> additional word in the seed phrase. Whitespace will <em>not</em> be trimmed.</param>
	/// <param name="mnemonic">Receives the mnemonic.</param>
	/// <param name="decodeError">Receives the error code if decoding fails.</param>
	/// <param name="errorMessage">Receives the error message if decoding fails.</param>
	/// <returns><see langword="true" /> if decoding succeeds; <see langword="false" /> otherwise.</returns>
	public static bool TryParse(ReadOnlySpan<char> seedPhrase, ReadOnlyMemory<char> password, [NotNullWhen(true)] out Bip39Mnemonic? mnemonic, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage)
	{
		WordList wordList = WordList.Default;
		int bitsInitialized = 0;
		int wordCount = CountWords(seedPhrase);
		if (wordCount % 3 > 0 || wordCount == 0)
		{
			mnemonic = null;
			decodeError = DecodeError.BadWordCount;
			errorMessage = Strings.WrongNumberOfWords;
			return false;
		}

		(int entropyLengthInBits, int checksumLengthInBits) = GetLengths(wordCount);

		Span<byte> entropyAndChecksum = stackalloc byte[(int)Math.Ceiling((double)(entropyLengthInBits + checksumLengthInBits) / 8)];
		ReadOnlySpan<byte> decodedEntropy = entropyAndChecksum[..(entropyLengthInBits / 8)];
		ReadOnlySpan<byte> decodedChecksum = entropyAndChecksum[(entropyLengthInBits / 8)..];

		while (true)
		{
			seedPhrase = seedPhrase.TrimStart();
			int wordBoundaryIdx = FindNextWordBoundary(seedPhrase);
			ReadOnlySpan<char> word = wordBoundaryIdx < 0 ? seedPhrase : seedPhrase[..wordBoundaryIdx];
			if (word.IsEmpty)
			{
				break;
			}

			int wordIndex = wordList.Find(word);
			if (wordIndex == -1)
			{
				mnemonic = null;
				decodeError = DecodeError.InvalidWord;
				errorMessage = Strings.FormatWordNotOnWordList(word.ToString());
				return false;
			}

			AppendBits(entropyAndChecksum, ref bitsInitialized, bits: wordIndex, bitCount: 11);
			seedPhrase = wordBoundaryIdx < 0 ? default : seedPhrase[(wordBoundaryIdx + 1)..];
		}

		// Verify the checksum.
		Span<byte> computedChecksum = stackalloc byte[SHA256.HashSizeInBytes];
		SHA256.HashData(entropyAndChecksum[..(entropyLengthInBits / 8)], computedChecksum);

		// Truncate the checksum in bytes, and also in bits since the checksum can be a fraction of a byte in length.
		computedChecksum = computedChecksum[..((int)Math.Ceiling((double)checksumLengthInBits / 8))];
		if (checksumLengthInBits % 8 > 0)
		{
			computedChecksum[^1] &= BitcoinUtilities.MaskMSB(checksumLengthInBits % 8);
		}

		if (!decodedChecksum.SequenceEqual(computedChecksum))
		{
			decodeError = DecodeError.InvalidChecksum;
			errorMessage = Strings.InvalidChecksum;
			mnemonic = null;
			return false;
		}

		mnemonic = new Bip39Mnemonic(decodedEntropy, password);
		decodeError = null;
		errorMessage = null;
		return true;
	}

	/// <summary>
	/// Gets the number of words required to represent a given entropy requirement.
	/// </summary>
	/// <param name="entropyLengthInBits">The minimum required length of entropy (in bits). If this is not a multiple of 32, it will be rounded up to meet BIP-39 algorithm requirements.</param>
	/// <returns>The number of words required in the seed phrase to meet that requirement.</returns>
	public static int WordsRequiredForEntropyLength(int entropyLengthInBits)
	{
		Requires.Range(entropyLengthInBits > 0, nameof(entropyLengthInBits));

		int mod = entropyLengthInBits % 32;
		if (mod > 0)
		{
			entropyLengthInBits += 32 - mod;
		}

		return (entropyLengthInBits + (entropyLengthInBits / 32)) / 11;
	}

	/// <summary>
	/// Returns the <see cref="SeedPhrase"/>.
	/// </summary>
	/// <returns>The seed phrase.</returns>
	public override string ToString() => this.SeedPhrase;

	/// <summary>
	/// Returns the first n words of the seed phrase.
	/// </summary>
	/// <param name="words">The number of words to include.</param>
	/// <returns>A shortened seed phrase.</returns>
	public string ToString(int words) => string.Join(' ', this.SeedPhrase.Split(' ').Take(words));

	/// <summary>
	/// Appends bits to a buffer.
	/// </summary>
	/// <param name="buffer">The buffer to append to.</param>
	/// <param name="bitsInitialized">The number of bits initialized in the buffer. This number will be incremented by <paramref name="bitCount"/> upon successful completion.</param>
	/// <param name="bits">An integer whose least significant <paramref name="bitCount"/> bits carry the data to append to <paramref name="buffer"/>.</param>
	/// <param name="bitCount">The number of bits in <paramref name="bits"/> to append to <paramref name="buffer"/>.</param>
	private static void AppendBits(Span<byte> buffer, ref int bitsInitialized, int bits, int bitCount)
	{
		Debug.Assert(bitCount > 0 && bitCount <= sizeof(int) * 8, "We cannot initialize 0 bits or more than fit in the bits parameter.");

		// Skip over the whole bytes that have already been initialized.
		buffer = buffer[(bitsInitialized / 8)..];
		int bitOffset = bitsInitialized % 8;

		while (bitCount > 0)
		{
			int uninitializedBits = 8 - bitOffset;
			int bitsToCopyThisRound = Math.Min(uninitializedBits, bitCount);

			int shiftedBits = bits;
			if (uninitializedBits > bitCount)
			{
				// Shift bits left if the current byte has more space than we have bits left.
				shiftedBits <<= uninitializedBits - bitCount;
			}
			else if (uninitializedBits < bitCount)
			{
				// Shift bits right if the current byte has less space than we have bits left.
				shiftedBits >>= bitCount - uninitializedBits;
			}

			buffer[0] |= (byte)shiftedBits;

			bitsInitialized += bitsToCopyThisRound;
			bitCount -= bitsToCopyThisRound;
			bitOffset = 0;
			buffer = buffer[1..];
		}
	}

	/// <summary>
	/// Gets a sequence of bits from a buffer.
	/// </summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <param name="bitsOffset">The number of bits in <paramref name="buffer"/> to skip over.</param>
	/// <param name="bitsLength">The number of bits to read. The allowed range is [0, 32].</param>
	/// <returns>The bits read from <paramref name="buffer"/>, aligned to the LSB edge.</returns>
	private static int GetBits(ReadOnlySpan<byte> buffer, int bitsOffset, int bitsLength)
	{
		Debug.Assert(bitsLength < sizeof(int) * 8, "We cannot return more bits than fit in a 32-bit integer.");

		// Skip over the whole bytes we needn't read.
		buffer = buffer[(bitsOffset / 8)..];
		bitsOffset %= 8;

		int result = 0;
		while (bitsLength > 0)
		{
			byte b = buffer[0];
			int validBits = 8 - bitsOffset;
			int useBits = Math.Min(validBits, bitsLength);
			int unusedLSBs = validBits - useBits;

			// Create a mask for exactly the set of bits we can use from the buffer.
			byte mask = (byte)(BitcoinUtilities.MaskLSB(useBits) << unusedLSBs);

			// Now move the bits we can use to the LSB position, truncating the excess LSB.
			b = (byte)((b & mask) >> unusedLSBs);

			// Now move the bits into their proper position in the result.
			result |= b << (bitsLength - useBits);

			bitsLength -= useBits;
			buffer = buffer[1..];
			bitsOffset = 0;
		}

		return result;
	}

	/// <summary>
	/// Counts the words in a given seed phrase.
	/// </summary>
	/// <param name="seedPhrase">The seed phrase.</param>
	/// <returns>The number of words (as defined by whitespace separation between sequences of characters).</returns>
	private static int CountWords(ReadOnlySpan<char> seedPhrase)
	{
		int wordCount = 0;
		while (seedPhrase.Length > 0)
		{
			seedPhrase = seedPhrase.TrimStart();
			int wordBoundaryIndex = FindNextWordBoundary(seedPhrase);
			if (wordBoundaryIndex == -1)
			{
				break;
			}

			wordCount++;
			seedPhrase = seedPhrase[wordBoundaryIndex..];
		}

		if (seedPhrase.Length > 0)
		{
			wordCount++;
		}

		return wordCount;
	}

	private static int FindNextWordBoundary(ReadOnlySpan<char> chars)
	{
		for (int i = 0; i < chars.Length; i++)
		{
			if (char.IsWhiteSpace(chars[i]))
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// Gets the lengths of the entropy and checksum for a BIP 39 seed phrase.
	/// </summary>
	/// <param name="wordCount">The number of words in the seed phrase.</param>
	/// <returns>The length (in bits) of the entropy and checksum.</returns>
	private static (int EntropyLengthInBits, int ChecksumLengthInBits) GetLengths(int wordCount)
	{
		int dataLengthInBits = wordCount * 11;
		int entropyLength = dataLengthInBits * 32 / 33;
		int checksumLength = dataLengthInBits - entropyLength;
		return (entropyLength, checksumLength);
	}

	private static ReadOnlySpan<char> GetNFKDChars(ReadOnlySpan<char> chars)
	{
		bool allAscii = true;
		for (int i = 0; i < chars.Length && allAscii; i++)
		{
			allAscii &= char.IsAscii(chars[i]);
		}

		return allAscii ? chars : chars.ToString().Normalize(NormalizationForm.FormKD);
	}

	private static void CreateBinarySeed(ReadOnlySpan<char> seedPhrase, ReadOnlySpan<char> password, Span<byte> binarySeed)
	{
		// The salt is "mnemonic{password}".
		ReadOnlySpan<byte> seedSaltPrefix = "mnemonic"u8;
		password = GetNFKDChars(password);
		Span<byte> salt = stackalloc byte[seedSaltPrefix.Length + BinarySeedEncoding.GetMaxByteCount(password.Length)];
		seedSaltPrefix.CopyTo(salt);
		int encodedPasswordLength = BinarySeedEncoding.GetBytes(password, salt[seedSaltPrefix.Length..]);
		salt = salt[..(seedSaltPrefix.Length + encodedPasswordLength)];

		Rfc2898DeriveBytes.Pbkdf2(GetNFKDChars(seedPhrase), salt, binarySeed, 2048, HashAlgorithmName.SHA512);
	}

	private static string CreateSeedPhrase(ReadOnlySpan<byte> entropy, WordList wordList)
	{
		int checksumLengthInBits = entropy.Length / 4;

		Span<byte> entropyAndChecksum = stackalloc byte[entropy.Length + SHA256.HashSizeInBytes];
		entropy.CopyTo(entropyAndChecksum);
		Span<byte> hash = entropyAndChecksum[entropy.Length..];
		SHA256.HashData(entropy, hash);

		int wordCount = ((entropy.Length * 8) + checksumLengthInBits) / 11;
		Span<ushort> wordIndexes = stackalloc ushort[wordCount];
		for (int wordNumber = 0; wordNumber < wordCount; wordNumber++)
		{
			int wordIdx = GetBits(entropyAndChecksum, wordNumber * 11, 11);
			wordIndexes[wordNumber] = (ushort)wordIdx;
		}

		Span<char> seedPhrase = stackalloc char[(wordList.LongestWord + 1) * wordIndexes.Length];
		int phraseLength = 0;
		for (int wordNumber = 0; wordNumber < wordIndexes.Length; wordNumber++)
		{
			ReadOnlySpan<char> word = wordList[wordIndexes[wordNumber]];
			word.CopyTo(seedPhrase[phraseLength..]);
			phraseLength += word.Length;
			seedPhrase[phraseLength] = ' ';
			phraseLength++;
		}

		phraseLength--; // remove trailing space
		return seedPhrase[..phraseLength].ToString();
	}

	private unsafe struct FixedArrays
	{
		/// <summary>
		/// This is the exact seed length as specified by BIP-39.
		/// </summary>
		internal const int SeedLengthInBytes = 512 / 8;

		// This value is somewhat arbitrary.
		internal const int MaxEntropyLengthInBytes = 512 / 8;

		private readonly byte entropyLength;

		private fixed byte entropy[MaxEntropyLengthInBytes];

		private fixed byte seed[SeedLengthInBytes];

		internal FixedArrays(int entropyLength)
		{
			Requires.Range(entropyLength <= MaxEntropyLengthInBytes, nameof(entropyLength));
			this.entropyLength = (byte)entropyLength;
		}

		[UnscopedRef]
		internal readonly ReadOnlySpan<byte> Entropy => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(this.entropy[0]), this.entropyLength);

		[UnscopedRef]
		internal readonly ReadOnlySpan<byte> Seed => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(this.seed[0]), SeedLengthInBytes);

		[UnscopedRef]
		internal Span<byte> EntropyWritable => MemoryMarshal.CreateSpan(ref this.entropy[0], this.entropyLength);

		[UnscopedRef]
		internal Span<byte> SeedWritable => MemoryMarshal.CreateSpan(ref this.seed[0], SeedLengthInBytes);
	}
}
