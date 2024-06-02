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
		/// An invalid argument was provided.
		/// </summary>
		InvalidArgument,

		/// <summary>
		/// An error was returned by the SQLite client.
		/// </summary>
		Sqlite,

		/// <summary>
		/// Insufficient funds are available for a given send.
		/// </summary>
		InsufficientFunds,
	}

	/// <summary>
	/// Gets the code that indicates the nature of the error.
	/// </summary>
	public ErrorCode Code { get; init; } = ErrorCode.Other;

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWalletException"/> class.
	/// </summary>
	/// <param name="ex">The interop exception to copy data from.</param>
	/// <param name="cancellationToken">The cancellation token that was passed to the native side.</param>
	/// <returns>A new instance of <see cref="LightWalletException"/>.</returns>
	internal static Exception Wrap(uniffi.LightWallet.LightWalletException ex, CancellationToken cancellationToken = default) => ex switch
	{
		uniffi.LightWallet.LightWalletException.InsufficientFunds x => new InsufficientFundsException(x),
		uniffi.LightWallet.LightWalletException.InvalidUri x => new LightWalletException(Strings.InvalidUri) { Code = ErrorCode.InvalidUri },
		uniffi.LightWallet.LightWalletException.InvalidArgument x => new LightWalletException(x.message) { Code = ErrorCode.InvalidArgument },
		uniffi.LightWallet.LightWalletException.SqliteClientException x => new LightWalletException(x.message) { Code = ErrorCode.Sqlite },
		uniffi.LightWallet.LightWalletException.Other x => new LightWalletException(x.message),
		uniffi.LightWallet.LightWalletException.Canceled x => new OperationCanceledException(Strings.OperationCanceled, x, cancellationToken),
		_ => new LightWalletException(Strings.UnknownErrorAcrossInteropBoundary),
	};
}
