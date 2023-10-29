// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App;

public interface IContactManager
{
	ReadOnlyObservableCollection<Contact> Contacts { get; }

	void Add(Contact contact);

	bool Remove(Contact contact);

	bool TryGetContact(ZcashAccount account, DiversifierIndex diversifierIndex, [NotNullWhen(true)] out Contact? contact)
	{
		foreach (Contact c in this.Contacts)
		{
			if (c.AssignedAddresses.TryGetValue(account, out Contact.AssignedSendingAddresses? assignment) && assignment.AssignedDiversifier.Equals(diversifierIndex))
			{
				contact = c;
				return true;
			}
		}

		contact = null;
		return false;
	}
}
