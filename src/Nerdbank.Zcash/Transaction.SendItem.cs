// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial record Transaction
{
	/// <summary>
	/// Describes an individual spend in a transaction.
	/// </summary>
	/// <param name="Amount">The amount spent.</param>
	/// <param name="ToAddress">The receiver of this ZEC.</param>
	/// <param name="Memo">The memo included for this recipient.</param>
	public record struct SendItem(ZcashAddress ToAddress, decimal Amount, in Memo Memo)
	{
		/// <summary>
		/// Gets the full UA that was used when spending this, as recorded in the private change memo.
		/// </summary>
		public UnifiedAddress? RecipientUA { get; init; }
	}
}
