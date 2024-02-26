// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial record Transaction
{
	/// <summary>
	/// Describes an individual note received in a transaction.
	/// </summary>
	/// <param name="ToAddress">The address the note is addressed to. This will be the specific diversified address used. If a <see cref="UnifiedAddress"/>, it will have only one receiver, even if the address used by the sender originally had multiple receivers.</param>
	/// <param name="Amount">The amount received.</param>
	/// <param name="Memo">The memo included in the note.</param>
	public record struct RecvItem(ZcashAddress ToAddress, decimal Amount, in Memo Memo)
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

		/// <summary>
		/// Checks whether this line item represents change going back to the sender.
		/// </summary>
		/// <param name="account">The account that 'owns' this line item.</param>
		/// <returns>A value indicating whether this line item should be considered change.</returns>
		public bool IsChange(ZcashAccount account) => this.Memo.MemoFormat != Zip302MemoFormat.MemoFormat.Message
			&& Requires.NotNull(account).AddressSendsToThisAccount(this.ToAddress);
	}
}
