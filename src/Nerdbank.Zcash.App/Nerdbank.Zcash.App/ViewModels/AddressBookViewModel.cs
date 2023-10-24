// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App.ViewModels;

public class AddressBookViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;

	[Obsolete("For design-time use only", error: true)]
	public AddressBookViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.Contacts.Add(new ContactViewModel { Name = "Andrew Arnott", Address = "t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF" });
		this.Contacts.Add(new ContactViewModel { Name = "Jason Arnott", Address = "u17kydrnuh9k8dqtud9qugel5ym835xqg8jk5czy2qcxea0zucru7d9w0c9hcq43898l2d993taaqh6vr0u6yskjnn582vyvu8qqk6qyme0z2vfgcclxatca7cx2f45v2n9zfd7hmkwlrw0wt38z9ua2yvgdnvppucyf2cfsxwlyfy339k" });
	}

	public AddressBookViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;
	}

	public ObservableCollection<ContactViewModel> Contacts { get; } = new();

	public string Title => "Address Book";

	public string NameColumnHeader => "Name";

	public string SendCommandColumnHeader => "Has Address";
}
