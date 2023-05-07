// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// An exception thrown when a cryptographic key cannot be created or used.
/// </summary>
public class InvalidKeyException : Exception
{
	/// <inheritdoc cref="InvalidKeyException(string?, Exception?)"/>
	public InvalidKeyException()
	{
	}

	/// <inheritdoc cref="InvalidKeyException(string?, Exception?)"/>
	public InvalidKeyException(string? message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidKeyException"/> class.
	/// </summary>
	/// <param name="message">The exception message.</param>
	/// <param name="inner">The inner exception.</param>
	public InvalidKeyException(string? message, Exception? inner)
		: base(message, inner)
	{
	}

	/// <summary>
	/// Gets the key derivationpath that failed, if applicable.
	/// </summary>
	public Bip32HDWallet.KeyPath? KeyPath { get; init; }
}
