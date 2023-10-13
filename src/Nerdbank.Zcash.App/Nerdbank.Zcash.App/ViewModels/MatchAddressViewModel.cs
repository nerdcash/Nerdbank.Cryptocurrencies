// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class MatchAddressViewModel : ViewModelBase
{
	private bool isAccountMatch;
	private bool isContactMatch;
	private AccountViewModel? matchedAccount;
	private ContactViewModel? matchedContact;

	public MatchAddressViewModel()
	{
		this.MatchAddressCommand = ReactiveCommand.Create(() => { });
	}

	public string Title => $"Match Address";

	public string Explanation => "Use this tool to find who owns a particular Zcash address. It can match on any of your own addresses, or on any of your contacts' addresses.";

	public string AddressCaption => "Zcash address:";

	public string Address { get; set; } = string.Empty;

	public string MatchAddressCommandCaption => "Find match";

	public ReactiveCommand<Unit, Unit> MatchAddressCommand { get; }

	public bool IsAccountMatch
	{
		get => this.isAccountMatch;
		set => this.RaiseAndSetIfChanged(ref this.isAccountMatch, value);
	}

	public bool IsContactMatch
	{
		get => this.isContactMatch;
		set => this.RaiseAndSetIfChanged(ref this.isContactMatch, value);
	}

	public AccountViewModel? MatchedAccount
	{
		get => this.matchedAccount;
		set => this.RaiseAndSetIfChanged(ref this.matchedAccount, value);
	}

	public ContactViewModel? MatchedContact
	{
		get => this.matchedContact;
		set => this.RaiseAndSetIfChanged(ref this.matchedContact, value);
	}

	public string MatchedContactTitle => "✅ Matched contact";

	public string MatchedAccountTitle => "✅ Matched account";
}
