// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class AccountViewModel : ViewModelBase, IViewModel<Account>
{
	private readonly ObservableAsPropertyHelper<SecurityAmount> balance;
	private bool areKeysRevealed;

	public AccountViewModel(Account account, IViewModelServices viewModelServices)
	{
		this.balance = account.WhenAnyValue(a => a.Balance.MainBalance).ToProperty(this, nameof(this.Balance));

		this.FullViewingKey = account.ZcashAccount.FullViewing?.UnifiedKey.TextEncoding;
		this.IncomingViewingKey = account.ZcashAccount.IncomingViewing.UnifiedKey.TextEncoding;
		this.Account = account;
		if (viewModelServices.Wallet.TryGetHDWallet(account, out HDWallet? wallet))
		{
			this.GroupName = wallet.Name;
		}
		else
		{
			this.GroupName = Strings.LoneAccountGroupName;
		}
	}

	Account IViewModel<Account>.Model => this.Account;

	public Account Account { get; }

	public string? GroupName { get; }

	public string Name
	{
		get => this.Account.Name;
		set
		{
			if (this.Account.Name != value)
			{
				this.Account.Name = value;
				this.RaisePropertyChanged();
			}
		}
	}

	public bool IsIndexVisible => this.Account.ZcashAccount.HDDerivation is not null;

	public string IndexCaption => "Account index: ";

	public uint? Index => this.Account.ZcashAccount.HDDerivation?.AccountIndex;

	public SecurityAmount Balance => this.balance.Value;

	public bool AreKeysRevealed
	{
		get => this.areKeysRevealed;
		set => this.RaiseAndSetIfChanged(ref this.areKeysRevealed, value);
	}

	public string FullViewingKeyCaption => "Full viewing key";

	public string? FullViewingKey { get; }

	public string IncomingViewingKeyCaption => "Incoming viewing key";

	public string IncomingViewingKey { get; }

	public string RevealKeysCommandCaption => "Reveal keys";
}
