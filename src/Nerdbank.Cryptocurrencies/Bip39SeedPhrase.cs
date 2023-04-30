// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// A seed phrase generator.
/// </summary>
public static partial class Bip39SeedPhrase
{
	/// <summary>
	/// Generates a seed phrase for a newly generated secret.
	/// </summary>
	/// <param name="entropyLengthInBits">The number of secret bits that must be represented by the seed phrase. Must be a multiple of 32.</param>
	/// <returns>The seed phrase.</returns>
	/// <exception cref="ArgumentException">Throw if <paramref name="entropyLengthInBits"/> is not a multiple of 32.</exception>
	public static string Generate(int entropyLengthInBits)
	{
		Requires.Argument(entropyLengthInBits % 32 == 0, nameof(entropyLengthInBits), Strings.MustBeNonZeroMultipleOf32);
		Span<byte> entropy = stackalloc byte[entropyLengthInBits / 8];
		RandomNumberGenerator.Fill(entropy);
		return Generate(entropy);
	}

	/// <summary>
	/// Generates a seed phrase for a given entropy buffer.
	/// </summary>
	/// <param name="entropy">The entropy buffer that must be represented by the seed phrase. The length in bytes must be a non-zero multiple of 4.</param>
	/// <returns>The seed phrase.</returns>
	/// <exception cref="ArgumentException">Throw if the length of <paramref name="entropy"/> is not a multiple of 4.</exception>
	public static string Generate(ReadOnlySpan<byte> entropy)
	{
		Requires.Argument(entropy.Length % 4 == 0 && entropy.Length > 0, nameof(entropy), Strings.LengthMustBeNonEmptyAndDivisibleBy4);
		int checksumLengthInBits = entropy.Length / 4;

		Span<byte> entropyAndChecksum = stackalloc byte[entropy.Length + SHA256.HashSizeInBytes];
		entropy.CopyTo(entropyAndChecksum);
		Span<byte> hash = entropyAndChecksum[entropy.Length..];
		SHA256.HashData(entropy, hash);

		WordList wordList = WordList.Default;
		int wordCount = ((entropy.Length * 8) + checksumLengthInBits) / 11;
		Span<char> seedPhrase = stackalloc char[(wordList.LongestWord + 1) * wordCount];
		int phraseLength = 0;
		for (int wordNumber = 0; wordNumber < wordCount; wordNumber++)
		{
			int wordIdx = GetBits(entropyAndChecksum, wordNumber * 11, 11);
			ReadOnlySpan<char> word = wordList[wordIdx];
			word.CopyTo(seedPhrase[phraseLength..]);
			phraseLength += word.Length;
			seedPhrase[phraseLength] = ' ';
			phraseLength++;
		}

		phraseLength--; // remove trailing space
		return seedPhrase[..phraseLength].ToString();
	}

	/// <summary>
	/// Gets the length of the the entropy buffer required to decode a given seed phrase.
	/// </summary>
	/// <param name="seedPhrase">The seed phrase.</param>
	/// <returns>The entropy length, in bits. Always a multiple of 32.</returns>
	public static int GetEntropyLengthInBits(ReadOnlySpan<char> seedPhrase)
	{
		int wordCount = CountWords(seedPhrase);

		(int entropyLength, _) = GetLengths(wordCount);
		return entropyLength;
	}

	/// <summary>
	/// Decodes a seed phrase to the entropy data it represents.
	/// </summary>
	/// <param name="seedPhrase">The seed phrase.</param>
	/// <param name="entropy">Receives the entropy data. The minimum required length for this can be obtained from <see cref="GetEntropyLengthInBits(ReadOnlySpan{char})"/>.</param>
	/// <param name="bytesWritten">Receives the number of bytes written to <paramref name="entropy"/>.</param>
	/// <param name="decodeError">Receives the error code if decoding fails.</param>
	/// <param name="errorMessage">Receives the error message if decoding fails.</param>
	/// <returns><see langword="true" /> if decoding succeeds; <see langword="false" /> otherwise.</returns>
	public static bool TryGetEntropy(ReadOnlySpan<char> seedPhrase, Span<byte> entropy, out int bytesWritten, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage)
	{
		WordList wordList = WordList.Default;
		int bitsInitialized = 0;
		int wordCount = CountWords(seedPhrase);
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
				bytesWritten = 0;
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
			computedChecksum[^1] &= BitUtilities.MaskMSB(checksumLengthInBits % 8);
		}

		if (!decodedChecksum.SequenceEqual(computedChecksum))
		{
			decodeError = DecodeError.InvalidChecksum;
			errorMessage = Strings.InvalidChecksum;
			bytesWritten = 0;
			return false;
		}

		entropyAndChecksum[..(entropyLengthInBits / 8)].CopyTo(entropy);
		bytesWritten = entropy.Length;
		decodeError = null;
		errorMessage = null;
		return true;
	}

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
			byte mask = (byte)(BitUtilities.MaskLSB(useBits) << unusedLSBs);

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
}
