// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;

namespace ViewModels;

public class MatchAddressViewModelTests : ViewModelTestBase
{
	private readonly Contact friend = new()
	{
		Name = "a friend",
	};

	private readonly ITestOutputHelper logger;
	private MatchAddressViewModel viewModel;
	private Account defaultAccount = null!; // set in InitializeAsync

	public MatchAddressViewModelTests(ITestOutputHelper logger)
	{
		this.viewModel = new MatchAddressViewModel(this.MainViewModel);
		this.logger = logger;
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();

		await this.InitializeWalletAsync();
		this.defaultAccount = this.App.Data.Wallet.Accounts.First();

		// Generate an account from which to produce a friend's unified address with at least 3 receivers.
		ZcashAccount friendsAccount = new(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), ZcashNetwork.TestNet));
		this.friend.ReceivingAddresses.Add(friendsAccount.DefaultAddress);

		Contact.AssignedSendingAddresses assignment = this.friend.GetOrCreateSendingAddressAssignment(this.defaultAccount);
		assignment.TransparentAddressIndex = 4;
		this.MainViewModel.ContactManager.Add(this.friend);
	}

	[UIFact]
	public void NoMatch_ValidAddress()
	{
		this.viewModel.Address = "u1ge2kww2a5fmlywt8pnetmgumatuw085kttxsu0fkuq9yxj8qtvnqtu0nskmsxtq9uk5a8q7xz9awevj90azgtk7p05g80yhwyfurkjnl3fs0a75548vnvgv3k96xsfutvezyp25g92d7lkzqrj2zjn9fhsuvm2l97892nveelvhlhsyr";
		Assert.True(this.viewModel.Match?.IsNoMatch);
		Assert.Null(this.viewModel.Match?.Account);
		Assert.Null(this.viewModel.Match?.Contact);
		Assert.Null(this.viewModel.Match?.DiversifiedAddressShownToContact);
	}

	[UIFact]
	public void InvalidAddress()
	{
		this.viewModel.Address = "u1ge2kww2a5";
		Assert.False(ValidateResults(this.viewModel, out IReadOnlyList<ValidationResult>? errors));
		Assert.NotEmpty(errors);
		foreach (ValidationResult error in errors)
		{
			this.logger.WriteLine(error.ErrorMessage);
		}
	}

	[UIFact]
	public void MatchOnAccount_NoObservingContact()
	{
		this.viewModel.Address = this.defaultAccount.ZcashAccount.DefaultAddress;
		Assert.Same(this.defaultAccount, this.viewModel.Match?.Account);
		Assert.Null(this.viewModel.Match?.DiversifiedAddressShownToContact);
		Assert.Null(this.viewModel.Match?.Contact);
		Assert.False(this.viewModel.Match?.IsNoMatch);
	}

	[UIFact]
	public void MatchOnAccount_WithObservingContact_Diversified()
	{
		DiversifierIndex idx = this.friend.AssignedAddresses[this.defaultAccount.Id!.Value].Diversifier;
		this.viewModel.Address = this.defaultAccount.ZcashAccount.GetDiversifiedAddress(ref idx);
		Assert.Same(this.defaultAccount, this.viewModel.Match?.Account);
		Assert.Same(this.friend, this.viewModel.Match?.DiversifiedAddressShownToContact);
		Assert.Null(this.viewModel.Match?.Contact);
		Assert.False(this.viewModel.Match?.IsNoMatch);
	}

	[UIFact]
	public void MatchOnAccount_WithObservingContact_Transparent()
	{
		uint idx = this.friend.AssignedAddresses[this.defaultAccount.Id!.Value].TransparentAddressIndex!.Value;
		this.viewModel.Address = this.defaultAccount.ZcashAccount.GetTransparentAddress(idx);
		Assert.Same(this.defaultAccount, this.viewModel.Match?.Account);
		Assert.Same(this.friend, this.viewModel.Match?.DiversifiedAddressShownToContact);
		Assert.Null(this.viewModel.Match?.Contact);
		Assert.False(this.viewModel.Match?.IsNoMatch);
	}

	/// <summary>
	/// Verifies that a match is found when the address is exactly what is in the address book.
	/// </summary>
	[UIFact]
	public void MatchOnContact_ExactMatch()
	{
		this.viewModel.Address = this.friend.ReceivingAddresses[0].Address;
		Assert.Same(this.friend, this.viewModel.Match?.Contact);
		Assert.Null(this.viewModel.Match?.Account);
		Assert.Null(this.viewModel.Match?.DiversifiedAddressShownToContact);
		Assert.False(this.viewModel.Match?.IsNoMatch);
	}

	/// <summary>
	/// Verifies that a match is found when the address has just one receiver in the contact's listed receiving address, which has several.
	/// </summary>
	[UIFact]
	public void MatchOnContact_MatchOneReceiverOfCompoundUnified()
	{
		UnifiedAddress friendUA = (UnifiedAddress)this.friend.ReceivingAddresses[0];
		this.viewModel.Address = new OrchardAddress(friendUA.GetPoolReceiver<OrchardReceiver>()!.Value, friendUA.Network);
		Assert.Same(this.friend, this.viewModel.Match?.Contact);
		Assert.Null(this.viewModel.Match?.Account);
		Assert.Null(this.viewModel.Match?.DiversifiedAddressShownToContact);
		Assert.False(this.viewModel.Match?.IsNoMatch);
	}

	/// <summary>
	/// Verifies that a match is found when the address is a compound unified address with a strict subset of the receivers in the contact's listed receiving address.
	/// </summary>
	[UIFact]
	public void MatchOnContact_MatchTwoReceiversOfCompoundUnified()
	{
		UnifiedAddress friendUA = (UnifiedAddress)this.friend.ReceivingAddresses[0];
		this.viewModel.Address = UnifiedAddress.Create(
			new OrchardAddress(friendUA.GetPoolReceiver<OrchardReceiver>()!.Value, friendUA.Network),
			new SaplingAddress(friendUA.GetPoolReceiver<SaplingReceiver>()!.Value, friendUA.Network));

		Assert.Same(this.friend, this.viewModel.Match?.Contact);
		Assert.Null(this.viewModel.Match?.Account);
		Assert.Null(this.viewModel.Match?.DiversifiedAddressShownToContact);
		Assert.False(this.viewModel.Match?.IsNoMatch);
	}

	/// <summary>
	/// Verifies that NO match is found when a UA is supplied that contains receivers that conflict with receivers in the listed address.
	/// </summary>
	[UIFact]
	public void MatchOnContact_ReceiverMismatch()
	{
		UnifiedAddress friendUA = (UnifiedAddress)this.friend.ReceivingAddresses[0];
		SaplingAddress strangerSapling = Zip32HDWallet.Sapling.Create(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), friendUA.Network).DefaultAddress;

		this.viewModel.Address = UnifiedAddress.Create(
			new OrchardAddress(friendUA.GetPoolReceiver<OrchardReceiver>()!.Value, friendUA.Network),
			strangerSapling);

		Assert.True(this.viewModel.Match?.IsNoMatch);
	}
}
