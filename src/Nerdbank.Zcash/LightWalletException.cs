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

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWalletException"/> class.
	/// </summary>
	/// <param name="ex">The interop exception to copy data from.</param>
	internal LightWalletException(uniffi.LightWallet.LightWalletException ex)
		: base(ex.Message, ex)
	{
		this.Code = ex switch
		{
			uniffi.LightWallet.LightWalletException.InvalidUri => ErrorCode.InvalidUri,
			uniffi.LightWallet.LightWalletException.InvalidHandle => ErrorCode.InvalidHandle,
			_ => ErrorCode.Other,
		};
	}

	/// <summary>
	/// Enumerates the error codes that may accompany a <see cref="LightWalletException"/>.
	/// </summary>
	public enum ErrorCode
	{
		/// <summary>
		/// Another error has occurred. See the <see cref="Exception.Message"/> and/or <see cref="Exception.InnerException"/> for details.
		/// </summary>
		Other,

		/// <summary>
		/// An invalid URI was provided.
		/// </summary>
		InvalidUri,

		/// <summary>
		/// An invalid handle was used.
		/// </summary>
		InvalidHandle,
	}

	/// <summary>
	/// Gets the code that indicates the nature of the error.
	/// </summary>
	public ErrorCode Code { get; init; } = ErrorCode.Other;
}
