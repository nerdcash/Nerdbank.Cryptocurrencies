// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// Thrown when a failure occurs in a <see cref="LightWalletClient"/> operation.
/// </summary>
public class LightWalletException : Exception
{
	/// <inheritdoc cref="LightWalletException(string?, Exception?)" />
	public LightWalletException(string? message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWalletException"/> class.
	/// </summary>
	/// <param name="message">An explanation of the problem.</param>
	/// <param name="innerException">The inner exception, if applicable.</param>
	public LightWalletException(string? message, Exception? innerException)
		: base(message, innerException)
	{
	}
}
