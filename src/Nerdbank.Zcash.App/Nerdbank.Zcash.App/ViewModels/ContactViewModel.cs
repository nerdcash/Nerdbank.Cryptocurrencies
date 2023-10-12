// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class ContactViewModel : ViewModelBase
{
	private string name = string.Empty;
	private string? address;
	private string? myAddressShownToContact;

	public ContactViewModel()
	{
		this.ShowDiversifiedAddressCommand = ReactiveCommand.Create(() => { });
		this.SendCommand = ReactiveCommand.Create(() => { });

		this.LinkProperty(nameof(this.Address), nameof(this.HasShieldedReceivingAddress));
		this.LinkProperty(nameof(this.Address), nameof(this.HasAddress));
		this.LinkProperty(nameof(this.MyAddressShownToContact), nameof(this.HasContactSeenMyDiversifiedAddressCaption));
		this.LinkProperty(nameof(this.MyAddressShownToContact), nameof(this.IsShowDiversifiedAddressButtonVisible));
		this.LinkProperty(nameof(this.HasShieldedReceivingAddress), nameof(this.SendCommandCaption));
	}

	/// <summary>
	/// Gets or sets the name of the contact.
	/// </summary>
	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	/// <summary>
	/// Gets or sets the Zcash address that may be used to send ZEC to the contact.
	/// </summary>
	public string? Address
	{
		get => this.address;
		set => this.RaiseAndSetIfChanged(ref this.address, value);
	}

	public string AddressCaption => "Address";

	public bool HasAddress => this.Address is not null && ZcashAddress.TryParse(this.Address, out _);

	/// <summary>
	/// Gets or sets an address from the user's wallet that was shared with the contact.
	/// </summary>
	/// <remarks>
	/// This is useful because at a low level, we can detect which diversified address was used to send money to the wallet,
	/// so by giving each contact a unique diversified address from this user's wallet, we can detect which contact sent money.
	/// </remarks>
	public string? MyAddressShownToContact
	{
		get => this.myAddressShownToContact;
		set => this.RaiseAndSetIfChanged(ref this.myAddressShownToContact, value);
	}

	public string HasContactSeenMyDiversifiedAddressCaption => this.MyAddressShownToContact is null ? "❌ This contact has not seen your diversified address." : "✅ This contact has seen your diversified address.";

	public bool IsShowDiversifiedAddressButtonVisible => this.MyAddressShownToContact is null;

	public string ShowDiversifiedAddressCommandCaption => "Share my diversified address with this contact";

	public ReactiveCommand<Unit, Unit> ShowDiversifiedAddressCommand { get; }

	public string SendCommandCaption => this.HasShieldedReceivingAddress ? "Send 🛡️" : "Send";

	public ReactiveCommand<Unit, Unit> SendCommand { get; }

	/// <summary>
	/// Gets a value indicating whether the contact has a shielded receiving address.
	/// </summary>
	public bool HasShieldedReceivingAddress => this.Address is not null && ZcashAddress.TryParse(this.Address, out ZcashAddress? address) && address.HasShieldedReceiver;
}
