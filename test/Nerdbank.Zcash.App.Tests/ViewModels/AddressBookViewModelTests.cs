// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
	public void ContactNamePersists()
	{
		ContactViewModel contact = this.viewModel.NewContact();
		contact.Name = "Somebody";

		// Re-open
		this.viewModel = new(this.MainViewModel);
		Assert.Equal("Somebody", this.viewModel.Contacts.Single().Name);
	}
}
