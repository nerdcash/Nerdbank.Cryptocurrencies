// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using DynamicData.Binding;
using static Nerdbank.Zcash.ZcashAddress.Match;

namespace Nerdbank.Zcash.App.ViewModels;

public class MatchAddressViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;
	private string address = string.Empty;
	private MatchResults? match;

	[Obsolete("For design-time use only", error: true)]
	public MatchAddressViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public MatchAddressViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;
		this.WhenAnyPropertyChanged(nameof(this.Address)).Subscribe(_ => this.DoMatch());
	}

	public string Title => $"Match Address";

	public string Explanation => "Use this tool to find who owns a particular Zcash address. It can match on any of your own addresses, or on any of your contacts' addresses.";

	public string AddressWatermark => "Zcash address";

	[Required, ZcashAddress]
	public string Address
	{
		get => this.address;
		set => this.RaiseAndSetIfChanged(ref this.address, value);
	}

	public MatchResults? Match
	{
		get => this.match;
		private set => this.RaiseAndSetIfChanged(ref this.match, value);
	}

	public string MatchedContactTitle => "✅ Matched contact";

	public string MatchedAccountTitle => "✅ Matched account";

	public string NoMatchTitle => "❌ No match";

	public string DiversifiedAddressShownToContactCaption => "This address was shown to this contact:";

	private void DoMatch()
	{
		if (ZcashAddress.TryDecode(this.Address, out _, out _, out ZcashAddress? address))
		{
			if (this.TryMatchOnAccount(address, out Account? account))
			{
				this.TryMatchOnObservingContact(account.ZcashAccount, address, out Contact? receivingContact);
				this.Match = new MatchResults(account, receivingContact);
			}
			else if (this.TryMatchOnContact(address, out Contact? contact, out string? caveats))
			{
				this.Match = new MatchResults(contact, caveats);
			}
			else
			{
				this.Match = MatchResults.NoMatch;
			}
		}
		else
		{
			this.Match = null;
		}
	}

	private bool TryMatchOnAccount(ZcashAddress address, [NotNullWhen(true)] out Account? account)
	{
		foreach (Account candidate in this.viewModelServices.Wallet.AllAccounts.SelectMany(g => g))
		{
			if (candidate.ZcashAccount.AddressSendsToThisAccount(address))
			{
				account = candidate;
				return true;
			}
		}

		account = null;
		return false;
	}

	private bool TryMatchOnObservingContact(ZcashAccount account, ZcashAddress address, [NotNullWhen(true)] out Contact? receivingContact)
	{
		foreach (Contact candidate in this.viewModelServices.ContactManager.Contacts)
		{
			if (candidate.AssignedAddresses.TryGetValue(account, out Contact.AssignedSendingAddresses? assigned))
			{
				if (address is TransparentAddress transparentAddr && assigned.AssignedTransparentAddressIndex is uint idx)
				{
					if (account.GetTransparentAddress(idx).Equals(transparentAddr))
					{
						receivingContact = candidate;
						return true;
					}
				}
				else
				{
					DiversifierIndex contactDiversifier = assigned.AssignedDiversifier;
					UnifiedAddress ua = account.GetDiversifiedAddress(ref contactDiversifier);
					ZcashAddress.Match match = ua.IsMatch(address);

					// Only consider it a match if we match all common receivers, and the test address does *not* include any extra receivers.
					if ((match & (MatchingReceiversFound | MismatchingReceiversFound | UniqueReceiverTypesInTestAddress)) == MatchingReceiversFound)
					{
						receivingContact = candidate;
						return true;
					}
				}
			}
		}

		receivingContact = null;
		return false;
	}

	private bool TryMatchOnContact(ZcashAddress address, [NotNullWhen(true)] out Contact? contact, out string? caveats)
	{
		caveats = null;
		ZcashAddress.Match match = this.viewModelServices.ContactManager.FindContact(address, out contact);

		if (contact is not null)
		{
			// Partial mismatches are too dangerous to report as any kind of match unless we have
			// the right UI to make it clear what happened.
			bool noMismatches = (match & (MatchingReceiversFound | MismatchingReceiversFound)) == MatchingReceiversFound;
			if (noMismatches)
			{
				if (match.HasFlag(UniqueReceiverTypesInTestAddress))
				{
					// The address the user provided has more receivers than the matching contact.
					// This may be harmless, or it may mean that the additional receivers do not actually belong
					// to the contact, and using this address could send ZEC to the wrong person.
					caveats = "This address has more receivers than the contact's address. Only use this larger address for the contact if you trust the source of the address.";
				}

				// No worries if the input address has fewer receivers than the contact.
				return true;
			}
		}

		contact = null;
		return false;
	}

	public class MatchResults
	{
		public static readonly MatchResults NoMatch = new();

		public MatchResults(Account account, Contact? diversifiedAddressShownToContact)
		{
			this.Account = account;
			this.DiversifiedAddressShownToContact = diversifiedAddressShownToContact;
		}

		public MatchResults(Contact contact, string? contactMatchCaveats)
		{
			this.Contact = contact;
			this.ContactMatchCaveats = contactMatchCaveats;
		}

		private MatchResults()
		{
		}

		public Account? Account { get; }

		public Contact? Contact { get; }

		public string? ContactMatchCaveats { get; }

		public Contact? DiversifiedAddressShownToContact { get; }

		public bool IsNoMatch => this.Account is null && this.Contact is null;
	}
}
