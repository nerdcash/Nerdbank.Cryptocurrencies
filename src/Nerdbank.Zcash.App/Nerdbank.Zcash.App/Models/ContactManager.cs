// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App.Models;

public class ContactManager : IContactManager
{
	public ObservableCollection<Contact> Contacts { get; } = new();
}
