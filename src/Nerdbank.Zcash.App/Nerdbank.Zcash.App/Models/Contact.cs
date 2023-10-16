// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.Models;

public class Contact
{
	public string Name { get; set; } = string.Empty;

	public ZcashAddress? ReceivingAddress { get; }

	/// <summary>
	/// Gets the addresses that have been assigned to this contact for sending to the wallet owner.
	/// </summary>
	/// <remarks>
	/// The assignments are unique for each account, because each account has its own transparent addresses
	/// and not all diversifiers are valid for sapling, so each account needs to pick its own.
	/// Finally, users on the contact list are only shown an address from a particular account,
	/// so it works out to record the address they saw on a per-account basis.
	/// </remarks>
	public Dictionary<ZcashAccount, AssignedSendingAddresses> AssignedAddresses { get; } = new();

	public AssignedSendingAddresses GetOrCreateSendingAddressAssignment(ZcashAccount account)
	{
		if (!this.AssignedAddresses.TryGetValue(account, out AssignedSendingAddresses? assignment))
		{
			DiversifierIndex diversifierIndex = new(DateTime.UtcNow.Ticks);

			// Sapling may force an adjustment to the diversifier index,
			// so let that happen before we record the assignment.
			account.GetDiversifiedAddress(ref diversifierIndex);
			assignment = new AssignedSendingAddresses()
			{
				AssignedDiversifier = diversifierIndex,
			};
			this.AssignedAddresses.Add(account, assignment);
		}

		return assignment;
	}

	public class AssignedSendingAddresses
	{
		public required DiversifierIndex AssignedDiversifier { get; set; }

		public uint? AssignedTransparentAddressIndex { get; set; }
	}
}
