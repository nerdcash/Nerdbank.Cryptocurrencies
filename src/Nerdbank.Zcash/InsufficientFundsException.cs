// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Zcash.ZcashUtilities;

namespace Nerdbank.Zcash;

/// <summary>
/// An exception thrown when a request to spend funds cannot be fulfilled due to insufficient funds.
/// </summary>
public class InsufficientFundsException : LightWalletException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InsufficientFundsException"/> class.
	/// </summary>
	public InsufficientFundsException()
		: base(Strings.InsufficientFunds)
	{
		this.Code = ErrorCode.InsufficientFunds;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InsufficientFundsException"/> class.
	/// </summary>
	/// <param name="ex">The interop exception to wrap.</param>
	[SetsRequiredMembers]
	internal InsufficientFundsException(uniffi.LightWallet.LightWalletException.InsufficientFunds ex)
		: base(Strings.FormatInsufficientFunds(ZatsToZEC(ex.required), ZatsToZEC(ex.available)), ex)
	{
		this.Code = ErrorCode.InsufficientFunds;
		this.AvailableBalance = ZatsToZEC(ex.available);
		this.RequiredBalance = ZatsToZEC(ex.required);
	}

	/// <summary>
	/// Gets the actual spendable balance.
	/// </summary>
	public required decimal AvailableBalance { get; init; }

	/// <summary>
	/// Gets the balance required for the operation.
	/// </summary>
	public required decimal RequiredBalance { get; init; }
}
