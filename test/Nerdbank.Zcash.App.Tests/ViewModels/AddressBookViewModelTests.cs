// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class AddressBookViewModelTests
{
	private MainViewModel mainViewModel = new();
	private AddressBookViewModel viewModel;

	public AddressBookViewModelTests()
	{
		this.viewModel = new AddressBookViewModel(this.mainViewModel);
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
	}
}
