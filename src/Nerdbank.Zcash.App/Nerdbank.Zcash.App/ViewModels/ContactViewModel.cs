// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace Nerdbank.Zcash.App.ViewModels;

public class ContactViewModel : ViewModelBase
{
	private readonly AddressBookViewModel addressBook;
	private readonly ObservableAsPropertyHelper<bool> isEmpty;
	private readonly ObservableAsPropertyHelper<bool> hasShieldedReceivingAddress;
	private readonly ObservableAsPropertyHelper<bool> hasAddress;
	private readonly ObservableAsPropertyHelper<string> sendCommandCaption;
	private readonly ObservableAsPropertyHelper<string> hasContactSeenMyDiversifiedAddressCaption;
	private readonly ObservableBox<bool> canSend = new(false);
	private string addresses = string.Empty;

	public ContactViewModel(AddressBookViewModel addressBook, Contact model)
	{
		this.addressBook = addressBook;
		this.Model = model;

		IObservable<bool> hasContactSeenMyDiversifiedAddress = this.WhenAnyValue(
			vm => vm.addressBook.SelectedAccount,
			vm => vm.Model.AssignedAddresses,
			(account, addresses) => account is not null && addresses.ContainsKey(account.Id!.Value));

		this.addresses = string.Join(' ', this.Model.ReceivingAddresses);

		IObservable<ZcashAddress[]> parsedReceivingAddresses = this.WhenAnyValue(
			vm => vm.Addresses,
			a => a?.Split(ZcashAddressAttribute.WhitespaceCharacters, StringSplitOptions.RemoveEmptyEntries).SelectMany(slice => ZcashAddress.TryDecode(slice, out _, out _, out ZcashAddress? address) ? new[] { address } : Array.Empty<ZcashAddress>()).ToArray() ?? Array.Empty<ZcashAddress>());

		IObservable<bool> hasValidAddress = parsedReceivingAddresses.Select(a => a.Length > 0);
		IObservable<bool> hasShieldedAddress = parsedReceivingAddresses.Select(a => a.Any(b => b.HasShieldedReceiver is true));

		this.hasContactSeenMyDiversifiedAddressCaption = hasContactSeenMyDiversifiedAddress.Select(v => v
			? ("✅ " + ContactStrings.ContactHasSeenDiversifiedAddress)
			: ("❌ " + ContactStrings.ContactHasNotSeenDiversifiedAddress))
			.ToProperty(this, nameof(this.HasContactSeenMyDiversifiedAddressCaption));

		this.isEmpty = this.WhenAnyValue(
			vm => vm.Name,
			vm => vm.Addresses,
			vm => vm.Model.AssignedAddresses,
			(n, addresses, assignments) => n.Length == 0 && string.IsNullOrWhiteSpace(addresses) && assignments.Count == 0)
			.ToProperty(this, nameof(this.IsEmpty));

		this.hasShieldedReceivingAddress = hasShieldedAddress
			.ToProperty(this, nameof(this.HasShieldedReceivingAddress));
		this.hasAddress = hasValidAddress
			.ToProperty(this, nameof(this.HasAddress));
		this.sendCommandCaption = hasShieldedAddress.Select(has => ContactStrings.SendCommandCaption + (has ? " 🛡️" : string.Empty))
			.ToProperty(this, nameof(this.SendCommandCaption));

		this.ShowDiversifiedAddressCommand = ReactiveCommand.Create(this.ShowDiversifiedAddress);

		this.UpdateCanSend();
		this.SendCommand = ReactiveCommand.Create(this.Send, this.canSend);

		this.Model.WhenAnyValue(c => c.Name).Subscribe(_ => this.RaisePropertyChanged(nameof(this.Name)));
	}

	public Contact Model { get; }

	public bool IsEmpty => this.isEmpty.Value;

	/// <summary>
	/// Gets or sets the name of the contact.
	/// </summary>
	public string Name
	{
		get => this.Model.Name;
		set
		{
			if (this.Model.Name != value)
			{
				this.Model.Name = value;
				this.RaisePropertyChanged();
			}
		}
	}

	/// <summary>
	/// Gets or sets a list of space-delimited Zcash addresses that may be used to send Zcash to the contact.
	/// </summary>
	[ZcashAddress(AllowMultiple = true)]
	public string Addresses
	{
		get => this.addresses;
		set
		{
			this.RaiseAndSetIfChanged(ref this.addresses, value);

			// Update the model if the value is valid.
			if (string.IsNullOrWhiteSpace(value))
			{
				this.Model.ReceivingAddresses.Clear();
			}
			else
			{
				HashSet<ZcashAddress> parsedAddresses = new();
				bool invalidAddressesFound = false;
				foreach (string slice in value.Split(ZcashAddressAttribute.WhitespaceCharacters, StringSplitOptions.RemoveEmptyEntries))
				{
					if (ZcashAddress.TryDecode(slice, out _, out _, out ZcashAddress? address))
					{
						parsedAddresses.Add(address);
					}
					else
					{
						invalidAddressesFound = true;
					}
				}

				if (!invalidAddressesFound)
				{
					this.Model.ReceivingAddresses.AddOrRemoveToMatch(parsedAddresses);
				}
			}

			this.UpdateCanSend();
		}
	}

	public string AddressCaption => ContactStrings.AddressCaption;

	public bool HasAddress => this.hasAddress.Value;

	public string HasContactSeenMyDiversifiedAddressCaption => this.hasContactSeenMyDiversifiedAddressCaption.Value;

	public bool IsShowDiversifiedAddressButtonVisible => true;

	public string ShowDiversifiedAddressCommandCaption => ContactStrings.ShowDiversifiedAddressCommandCaption;

	public ReactiveCommand<Unit, ReceivingViewModel> ShowDiversifiedAddressCommand { get; }

	public string SendCommandCaption => this.sendCommandCaption.Value;

	public ReactiveCommand<Unit, SendingViewModel> SendCommand { get; }

	/// <summary>
	/// Gets a value indicating whether the contact has a shielded receiving address.
	/// </summary>
	public bool HasShieldedReceivingAddress => this.hasShieldedReceivingAddress.Value;

	private IViewModelServices ViewModelServices => this.addressBook.ViewModelServices;

	public SendingViewModel Send()
	{
		SendingViewModel viewModel = new(this.ViewModelServices);
		ZcashNetwork network = this.addressBook.SelectedAccount?.Network ?? throw new InvalidOperationException();
		viewModel.LineItems[0].SelectedRecipient = this.Model;
		return this.ViewModelServices.NavigateTo(viewModel);
	}

	public ReceivingViewModel ShowDiversifiedAddress() => this.ViewModelServices.NavigateTo(new ReceivingViewModel(
		this.ViewModelServices,
		this.addressBook.SelectedAccount,
		this.Model,
		paymentRequestDetailsViewModel: null));

	internal void OnSelectedAccountChanged()
	{
		this.UpdateCanSend();
	}

	private void UpdateCanSend()
	{
		this.canSend.Value = this.addressBook.SelectedAccount?.Network is ZcashNetwork net && this.Model.ReceivingAddresses.Any(a => a.Network == net);
	}
}
