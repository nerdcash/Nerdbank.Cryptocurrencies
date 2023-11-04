// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App;

public interface IContactManager
{
	ReadOnlyObservableCollection<Contact> Contacts { get; }

	void Add(Contact contact);

	bool Remove(Contact contact);

	/// <summary>
	/// Searches for a contact that has been shown a particular diversified address from the given account.
	/// </summary>
	/// <param name="account">The account whose receiving address has been shown to the matching contact.</param>
	/// <param name="diversifierIndex">The diversifier index used to generate the receiving address that has been shown to the matching contact.</param>
	/// <param name="contact">Receives the matching contact, if one is found.</param>
	/// <returns><see langword="true" /> if a matching contact was found; otherwise <see langword="false" />.</returns>
	bool TryGetContact(Account account, DiversifierIndex diversifierIndex, [NotNullWhen(true)] out Contact? contact)
	{
		foreach (Contact c in this.Contacts)
		{
			if (c.AssignedAddresses.TryGetValue(account, out Contact.AssignedSendingAddresses? assignment) && assignment.Diversifier.Equals(diversifierIndex))
			{
				contact = c;
				return true;
			}
		}

		contact = null;
		return false;
	}

	/// <summary>
	/// Searches for a contact that has a particular receiving address.
	/// </summary>
	/// <param name="receivingAddress">The receiving address that a matching contact must have.</param>
	/// <param name="contact">Receives the matching contact, if found.</param>
	/// <returns>Describes the confidence in the matching contact found, or indicates no match was found.</returns>
	ZcashAddress.Match FindContact(ZcashAddress receivingAddress, out Contact? contact)
	{
		foreach (Contact candidate in this.Contacts)
		{
			if (candidate.ReceivingAddress is not null)
			{
				ZcashAddress.Match match = candidate.ReceivingAddress.IsMatch(receivingAddress);
				if (match.HasFlag(ZcashAddress.Match.MatchingReceiversFound))
				{
					contact = candidate;
					return match;
				}
			}
		}

		contact = null;
		return ZcashAddress.Match.NoMatchingReceiverTypes;
	}
}
