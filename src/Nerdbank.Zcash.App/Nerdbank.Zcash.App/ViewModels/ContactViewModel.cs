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
	private string address = string.Empty;

	public ContactViewModel(AddressBookViewModel addressBook, Contact model)
	{
		this.addressBook = addressBook;
		this.Model = model;

		IObservable<bool> hasContactSeenMyDiversifiedAddress = this.WhenAnyValue(
			vm => vm.addressBook.SelectedAccount,
			vm => vm.Model.AssignedAddresses,
			(account, addresses) => account is not null && addresses.ContainsKey(account));

		IObservable<ZcashAddress?> parsedReceivingAddress = this.WhenAnyValue(
			vm => vm.Address,
			a => a.Length > 0 && ZcashAddress.TryDecode(a, out _, out _, out ZcashAddress? address) ? address : null);

		IObservable<bool> hasValidAddress = parsedReceivingAddress.Select(a => a is not null);
		IObservable<bool> hasShieldedAddress = parsedReceivingAddress.Select(a => a?.HasShieldedReceiver is true);

		this.hasContactSeenMyDiversifiedAddressCaption = hasContactSeenMyDiversifiedAddress.Select(v => v
			? "✅ This contact has seen your diversified address."
			: "❌ This contact has not seen your diversified address.")
			.ToProperty(this, nameof(this.HasContactSeenMyDiversifiedAddressCaption));

		this.isEmpty = this.WhenAnyValue(
			vm => vm.Name,
			vm => vm.Address,
			vm => vm.Model.AssignedAddresses,
			(n, a, assignments) => n.Length == 0 && a.Length == 0 && assignments.Count == 0)
			.ToProperty(this, nameof(this.IsEmpty));

		this.hasShieldedReceivingAddress = hasShieldedAddress
			.ToProperty(this, nameof(this.HasShieldedReceivingAddress));
		this.hasAddress = hasValidAddress
			.ToProperty(this, nameof(this.HasAddress));
		this.sendCommandCaption = hasShieldedAddress.Select(has => has ? "Send 🛡️" : "Send")
			.ToProperty(this, nameof(this.SendCommandCaption));

		this.ShowDiversifiedAddressCommand = ReactiveCommand.Create(this.ShowDiversifiedAddress);

		this.SendCommand = ReactiveCommand.Create(this.Send, hasValidAddress);

		this.Model.WhenAnyValue(c => c.Name).Subscribe(_ => this.RaisePropertyChanged(nameof(this.Name)));
		this.Address = this.Model.ReceivingAddress ?? string.Empty;
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
	/// Gets or sets the Zcash address that may be used to send ZEC to the contact.
	/// </summary>
	[ZcashAddress]
	public string Address
	{
		get => this.address;
		set
		{
			this.RaiseAndSetIfChanged(ref this.address, value);

			// Update the model if the value is valid.
			if (string.IsNullOrWhiteSpace(value))
			{
				this.Model.ReceivingAddress = null;
			}
			else if (ZcashAddress.TryDecode(value, out _, out _, out _))
			{
				this.Model.ReceivingAddress = ZcashAddress.Decode(value);
			}
		}
	}

	public string AddressCaption => "Address";

	public bool HasAddress => this.hasAddress.Value;

	public string HasContactSeenMyDiversifiedAddressCaption => this.hasContactSeenMyDiversifiedAddressCaption.Value;

	public bool IsShowDiversifiedAddressButtonVisible => true;

	public string ShowDiversifiedAddressCommandCaption => "Share my diversified address with this contact";

	public ReactiveCommand<Unit, ReceivingViewModel> ShowDiversifiedAddressCommand { get; }

	public string SendCommandCaption => this.sendCommandCaption.Value;

	public ReactiveCommand<Unit, SendingViewModel> SendCommand { get; }

	/// <summary>
	/// Gets a value indicating whether the contact has a shielded receiving address.
	/// </summary>
	public bool HasShieldedReceivingAddress => this.hasShieldedReceivingAddress.Value;

	private IViewModelServices ViewModelServices => this.addressBook.ViewModelServices;

	public SendingViewModel Send() =>
		this.ViewModelServices.NavigateTo(new SendingViewModel(this.ViewModelServices)
		{
			RecipientAddress = this.Address,
		});

	public ReceivingViewModel ShowDiversifiedAddress() => this.ViewModelServices.NavigateTo(new ReceivingViewModel(
		this.ViewModelServices,
		this.addressBook.SelectedAccount,
		this.Model,
		paymentRequestDetailsViewModel: null));
}
