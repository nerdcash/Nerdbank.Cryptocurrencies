// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace ViewModels;

public class AddressBookViewModelTests : ViewModelTestBase
{
	private AddressBookViewModel viewModel;

	public AddressBookViewModelTests()
	{
		this.viewModel = new AddressBookViewModel(this.MainViewModel);
	}

	[Fact]
	public void NewContact()
	{
		Assert.Null(this.viewModel.SelectedContact);
		this.viewModel.NewContact();
		Assert.NotNull(this.viewModel.SelectedContact);
		ContactViewModel newContact = this.viewModel.SelectedContact;
		Assert.True(this.viewModel.SelectedContact.IsEmpty);

		this.viewModel.NewContact();
		Assert.Same(newContact, Assert.Single(this.viewModel.Contacts));
		Assert.Same(newContact, this.viewModel.SelectedContact);

		newContact.Name = "Foo";
		this.viewModel.NewContact();
		Assert.NotSame(newContact, this.viewModel.SelectedContact);
		Assert.Equal(2, this.viewModel.Contacts.Count);
	}

	[Fact]
	public async Task RemoveContactAsync()
	{
		ContactViewModel contact = this.viewModel.NewContact();
		contact.Name = "Somebody";
		Assert.Single(this.MainViewModel.ContactManager.Contacts);

		await this.viewModel.DeleteContactCommand.Execute().FirstAsync();
		Assert.Empty(this.MainViewModel.ContactManager.Contacts);
	}

	[Fact]
	public void Contact_IsEmpty()
	{
		ContactViewModel contact = this.viewModel.NewContact();
		Assert.True(contact.IsEmpty);
		contact.Name = "Somebody";
		Assert.False(contact.IsEmpty);
	}

	[Fact]
	public void NewContactRemovedWhenSelectionChangesIfStillEmpty()
	{
		ContactViewModel contact1 = this.viewModel.NewContact();
		contact1.Name = "somebody";

		ContactViewModel contact2 = this.viewModel.NewContact();
		this.viewModel.SelectedContact = contact1;

		Assert.Same(contact1, Assert.Single(this.viewModel.Contacts));
		Assert.Same(contact1.Model, Assert.Single(this.MainViewModel.ContactManager.Contacts));
	}

	[Fact]
	public void PropertiesPersist()
	{
		const string name = "Somebody";
		const string address = "u1vmgf0qhsr0wgn94j8kuzk9ax93c26gs5h39sdsnpn3qg2regvdwjzfzvyg36lg69eds4l6u7ewrh3lt7wrl5p5wax6dgshewwrpanfz3wxd55zayzcels34rdxc3mcwgu9hf4sr2wt6f33crkkwp7d2xpjwlrfetj0y6d2pnhcar0cwz";
		ContactViewModel contact = this.viewModel.NewContact();
		contact.Name = name;
		contact.Address = address;

		// Re-open
		this.viewModel = new(this.MainViewModel);
		ContactViewModel reloadedContact = this.viewModel.Contacts.Single();
		Assert.Equal(name, reloadedContact.Name);
		Assert.Equal(address, reloadedContact.Address);
	}

	[Fact]
	public async Task AddressAssignmentMade()
	{
		await this.InitializeWalletAsync();

		ContactViewModel contact = this.viewModel.NewContact();
		contact.Name = "Somebody";

		// Navigate to the receiving address that is personalized for this particular contact.
		ReceivingViewModel receiving = await contact.ShowDiversifiedAddressCommand.Execute().FirstAsync();
		this.MainViewModel.NavigateBack();

		contact = Assert.Single(this.viewModel.Contacts);
		Assert.Single(contact.Model.AssignedAddresses);
	}

	[Fact]
	public void ChangeToInvalidZcashAddressIgnored()
	{
		const string name = "Somebody";
		const string address = "u1vmgf0qhsr0wgn94j8kuzk9ax93c26gs5h39sdsnpn3qg2regvdwjzfzvyg36lg69eds4l6u7ewrh3lt7wrl5p5wax6dgshewwrpanfz3wxd55zayzcels34rdxc3mcwgu9hf4sr2wt6f33crkkwp7d2xpjwlrfetj0y6d2pnhcar0cwz";
		ContactViewModel contact = this.viewModel.NewContact();
		contact.Name = name;
		contact.Address = address;
		contact.Address = "invalid";

		// Re-open
		this.viewModel = new(this.MainViewModel);
		ContactViewModel reloadedContact = this.viewModel.Contacts.Single();
		Assert.Equal(name, reloadedContact.Name);
		Assert.Equal(address, reloadedContact.Address);

		// Clear the address and confirm that it's gone.
		reloadedContact.Address = string.Empty;
		this.viewModel = new(this.MainViewModel);
		reloadedContact = this.viewModel.Contacts.Single();
		Assert.Equal(string.Empty, reloadedContact.Address);
	}
}
