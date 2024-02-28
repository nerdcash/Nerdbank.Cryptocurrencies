// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial record Transaction
{
	/// <summary>
	/// Describes an individual line item (a spend or a receive) in a transaction.
	/// </summary>
	/// <param name="Amount">The amount spent.</param>
	/// <param name="ToAddress">The receiver of this ZEC.</param>
	/// <param name="Memo">The memo included for this recipient.</param>
	public record struct LineItem(ZcashAddress ToAddress, decimal Amount, in Memo Memo)
	{
		/// <summary>
		/// Gets the pool that received this note.
		/// </summary>
		public Pool Pool
		{
			get => this.ToAddress switch
			{
				TransparentAddress => Pool.Transparent,
				SaplingAddress => Pool.Sapling,
				OrchardAddress => Pool.Orchard,
				null => throw new InvalidOperationException("This struct hasn't been initialized."),
				_ => throw new NotSupportedException(),
			};
		}
	}
}
