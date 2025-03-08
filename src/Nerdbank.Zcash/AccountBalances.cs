// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Zcash.ZcashUtilities;

namespace Nerdbank.Zcash;

#pragma warning disable SA1200 // Using directives should be placed correctly -- required for iOS
using Security = Nerdbank.Cryptocurrencies.Security;
#pragma warning restore SA1200 // Using directives should be placed correctly

/// <summary>
/// Describes account balances in a user-friendly way.
/// </summary>
public record AccountBalances
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AccountBalances"/> class.
	/// </summary>
	public AccountBalances()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AccountBalances"/> class.
	/// </summary>
	/// <param name="security">The security that these balances are measured in.</param>
	/// <param name="balances">The balances from the native module to copy from.</param>
	[SetsRequiredMembers]
	internal AccountBalances(Security security, uniffi.LightWallet.UserBalances balances)
	{
		this.Spendable = security.Amount(ZatsToZEC(balances.spendable));
		this.ImmatureChange = security.Amount(ZatsToZEC(balances.immatureChange));
		this.MinimumFees = -security.Amount(ZatsToZEC(balances.minimumFees));
		this.ImmatureIncome = security.Amount(ZatsToZEC(balances.immatureIncome));
		this.Dust = security.Amount(ZatsToZEC(balances.dust));
		this.Incoming = security.Amount(ZatsToZEC(balances.incoming));
		this.IncomingDust = security.Amount(ZatsToZEC(balances.incomingDust));
	}

	/// <summary>
	/// Gets the main balance to display to the user.
	/// </summary>
	/// <value>
	/// This is the sum of <see cref="Spendable"/>, <see cref="MinimumFees"/>, <see cref="ImmatureChange"/>, and <see cref="ImmatureIncome"/>.
	/// </value>
	/// <remarks>
	/// The other high-level balance to show is usually <see cref="Incoming"/>, if it is positive.
	/// </remarks>
	public SecurityAmount MainBalance => this.Spendable + this.MinimumFees + this.ImmatureChange + this.ImmatureIncome;

	/// <summary>
	/// Gets the funds available for immediate spending.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Expected fees are *not* deducted from this value, but the app may do so by subtracting `minimum_fees`.
	/// `dust` is excluded from this value.
	/// </para>
	/// <para>
	/// For enhanced privacy, the minimum number of required confirmations to spend a note is usually greater than one.
	/// This value is controlled by <see cref="LightWalletClient.MinimumConfirmations"/>.
	/// </para>
	/// </remarks>
	public SecurityAmount Spendable { get; init; }

	/// <summary>
	/// Gets the sum of the change notes that have insufficient confirmations to be spent.
	/// </summary>
	public SecurityAmount ImmatureChange { get; init; }

	/// <summary>
	/// Gets the minimum fees that can be expected to spend all `spendable + immature_change` funds in the wallet,
	/// expressed as a negative value.
	/// This fee assumes all funds will be sent to a single note.
	/// </summary>
	/// <remarks>
	/// Balances described by other fields in this struct are not included because they are not confirmed,
	/// they may amount to dust, or because as `immature` funds they may require shielding which has a cost
	/// and can change the amount of fees required to spend them (e.g. 3 UTXOs shielded together become only 1 note).
	/// </remarks>
	public SecurityAmount MinimumFees { get; init; }

	/// <summary>
	/// Gets the sum of non-change notes with a non-zero confirmation count that is less than the minimum required for spending,
	/// and all UTXOs (considering that UTXOs must be shielded before spending).
	/// `dust` is excluded from this value.
	/// </summary>
	/// <remarks>
	/// As funds mature, this may not be the exact amount added to `spendable`, since the process of maturing
	/// may require shielding, which has a cost.
	/// </remarks>
	public SecurityAmount ImmatureIncome { get; init; }

	/// <summary>
	/// Gets the sum of all *confirmed* UTXOs and notes that are worth less than the fee to spend them,
	/// making them essentially inaccessible.
	/// </summary>
	public SecurityAmount Dust { get; init; }

	/// <summary>
	/// Gets the sum of all *unconfirmed* UTXOs and notes that are not change.
	/// This value includes any applicable `incoming_dust`.
	/// </summary>
	public SecurityAmount Incoming { get; init; }

	/// <summary>
	/// Gets the sum of all *unconfirmed* UTXOs and notes that are not change and are each counted as dust.
	/// </summary>
	public SecurityAmount IncomingDust { get; init; }
}
