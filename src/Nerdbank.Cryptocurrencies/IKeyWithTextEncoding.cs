// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// A cryptocurrency key with a standard text encoding.
/// </summary>
public interface IKeyWithTextEncoding : IKey
{
	/// <summary>
	/// Gets the text encoding of this key.
	/// </summary>
	/// <remarks>
	/// To instantiate the key from this encoding, use <see cref="TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>.
	/// </remarks>
	string TextEncoding { get; }

	/// <summary>
	/// Deserializes the text encoding of a key.
	/// </summary>
	/// <param name="encoding">The text encoding of the key.</param>
	/// <param name="decodeError">If unsuccessful, receives the error code that identifies the first failure encountered during decoding.</param>
	/// <param name="errorMessage">If unsuccessful, receives the error message that explains the first failure encountered during decoding.</param>
	/// <param name="key">If successful, receives the deserialized key.</param>
	/// <returns><see langword="true" /> if decoding was successful; otherwise <see langword="false" />.</returns>
	static abstract bool TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key);
}
