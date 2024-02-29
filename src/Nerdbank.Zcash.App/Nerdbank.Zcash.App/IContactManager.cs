// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App;

public interface IContactManager
{
	ReadOnlyObservableCollection<Contact> Contacts { get; }

	int Add(Contact contact);

	bool Remove(Contact contact);

	bool TryGetContact(int id, [NotNullWhen(true)] out Contact? contact)
	{
		contact = this.Contacts.FirstOrDefault(c => c.Id == id);
		return contact is not null;
	}

	/// <summary>
	/// Searches for a contact that has been shown a particular address a the given account.
	/// </summary>
	/// <param name="account">The account whose receiving address has been shown to the matching contact.</param>
	/// <param name="sendingAddress">The address that may have been assigned to one of the contacts in the address book for purposes of sending funds into this wallet.</param>
	/// <param name="contact">Receives the matching contact, if one is found.</param>
	/// <returns><see langword="true" /> if a matching contact was found; otherwise <see langword="false" />.</returns>
	bool TryGetContact(Account account, ZcashAddress sendingAddress, [NotNullWhen(true)] out Contact? contact)
	{
		account.ZcashAccount.TryGetDiversifierIndex(sendingAddress, out DiversifierIndex? index);

		foreach (Contact c in this.Contacts)
		{
			if (c.AssignedAddresses.TryGetValue(account, out Contact.AssignedSendingAddresses? assignment))
			{
				if (index is not null && assignment.Diversifier.Equals(index.Value))
				{
					contact = c;
					return true;
				}

				if (assignment.TransparentAddressIndex is uint transparentIndex)
				{
					TransparentAddress tAddr = account.ZcashAccount.GetTransparentAddress(transparentIndex);
					if (sendingAddress.IsMatch(tAddr).HasFlag(ZcashAddress.Match.MatchingReceiversFound))
					{
						contact = c;
						return true;
					}
				}
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
			foreach (ZcashAddress addr in candidate.ReceivingAddresses)
			{
				ZcashAddress.Match match = addr.IsMatch(receivingAddress);
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

	/// <summary>
	/// Searches for a contact with an exact name match (case-insensitive).
	/// </summary>
	/// <param name="name">The name to search for.</param>
	/// <returns>The contact, if found.</returns>
	Contact? FindContact(string name)
	{
		foreach (Contact candidate in this.Contacts)
		{
			if (candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return candidate;
			}
		}

		return null;
	}
}
