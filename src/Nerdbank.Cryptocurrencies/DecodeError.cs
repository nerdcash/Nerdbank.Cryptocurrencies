// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// The failure modes that may occur while decoding.
/// </summary>
public enum DecodeError
{
	/// <summary>
	/// A disallowed character was found in the encoded string.
	/// </summary>
	InvalidCharacter,

	/// <summary>
	/// The checksum failed to match.
	/// </summary>
	InvalidChecksum,

	/// <summary>
	/// The buffer to decode into was too small.
	/// </summary>
	BufferTooSmall,

	/// <summary>
	/// The encoded string is missing a required separator.
	/// </summary>
	NoSeparator,

	/// <summary>
	/// The input was not the correct length for the specified encoding or the padding was not the expected value.
	/// </summary>
	BadPadding,

	/// <summary>
	/// A word was encountered that was not in the dictionary of allowed words for this encoding.
	/// </summary>
	InvalidWord,

	/// <summary>
	/// A phrase had an unexpected number of words in it.
	/// </summary>
	BadWordCount,

	/// <summary>
	/// The input was or led to a data length that was not expected.
	/// </summary>
	UnexpectedLength,

	/// <summary>
	/// The version header was not recognized.
	/// </summary>
	UnrecognizedVersion,

	/// <summary>
	/// The key could not be created.
	/// </summary>
	InvalidKey,

	/// <summary>
	/// The key carries inconsistent data regarding its derivation.
	/// </summary>
	InvalidDerivationData,
}
