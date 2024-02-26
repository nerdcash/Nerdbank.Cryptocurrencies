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
	/// <param name="IsChange">A value indicating whether this note represents "change" returned to the wallet in an otherwise outbound transaction.</param>
	public record struct RecvItem(ZcashAddress ToAddress, decimal Amount, in Memo Memo, bool IsChange)
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
