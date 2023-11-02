// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
namespace Nerdbank.Zcash.App.ViewModels;

public class MatchAddressViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;
	private bool isAccountMatch;
	private bool isContactMatch;
	private AccountViewModel? matchedAccount;
	private ContactViewModel? matchedContact;

	[Obsolete("For design-time use only", error: true)]
	public MatchAddressViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public MatchAddressViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;
		this.MatchAddressCommand = ReactiveCommand.Create(this.DoMatch);
	}

	public string Title => $"Match Address";

	public string Explanation => "Use this tool to find who owns a particular Zcash address. It can match on any of your own addresses, or on any of your contacts' addresses.";

	public string AddressCaption => "Zcash address:";

	[Required, ZcashAddress]
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

	private void DoMatch()
	{
		// Match on accounts first.
		// If we encounter a partial match (one where a multi-receiver UA has receivers that do match and receivers that don't), report a partial match with a big warning,
		// since this could mean an attacker is trying to trick the user into thinking they own the address.

		// Match on contacts next. When matching, try to match on each receiver.
		// On partial matches (e.g. the contact only has one receiver but we're searching with a multi-receiver unified address),
		// report a partial match and possibly offer to add the additional receivers to the contact if they trust the address.

		// On no match at all, show breakdown of receiver addresses if it's a multi-receiver address.
	}
}
