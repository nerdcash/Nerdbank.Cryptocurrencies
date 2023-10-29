// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using DynamicData;

namespace Nerdbank.Zcash.App.ViewModels;

public class AddressBookViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private ContactViewModel? selectedContact;

	[Obsolete("For design-time use only", error: true)]
	public AddressBookViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public AddressBookViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.Contacts.AddRange(viewModelServices.ContactManager.Contacts.Select(c => new ContactViewModel(this, c)));
		IObservable<bool> contactSelected = this.ObservableForProperty(vm => vm.SelectedContact, c => c is not null);

		this.NewContactCommand = ReactiveCommand.Create(this.NewContact);
		this.DeleteContactCommand = ReactiveCommand.Create(() => this.DeleteContact(this.SelectedContact!), contactSelected);
	}

	public ObservableCollection<ContactViewModel> Contacts { get; } = new();

	public string Title => "Address Book";

	public string NameColumnHeader => "Name";

	public string SendCommandColumnHeader => "Has Address";

	public ReactiveCommand<Unit, ContactViewModel> NewContactCommand { get; }

	public string NewContactCaption => "New";

	public string DeleteContactCaption => "Delete";

	public ReactiveCommand<Unit, Unit> DeleteContactCommand { get; }

	public ContactViewModel? SelectedContact
	{
		get => this.selectedContact;
		set => this.RaiseAndSetIfChanged(ref this.selectedContact, value);
	}

	public ContactViewModel NewContact()
	{
		ContactViewModel? newContact = this.Contacts.FirstOrDefault(c => c.IsEmpty);
		if (newContact is null)
		{
			Contact model = new();
			this.ViewModelServices.ContactManager.Add(model);
			newContact = new ContactViewModel(this, model);
			this.Contacts.Add(newContact);
		}

		this.SelectedContact = newContact;
		return newContact;
	}

	public void DeleteContact(ContactViewModel contact)
	{
		this.Contacts.Remove(contact);
	}
}
