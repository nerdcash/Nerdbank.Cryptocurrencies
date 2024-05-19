﻿// Copyright (c) Andrew Arnott. All rights reserved.
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
			this.GroupName = AccountStrings.LoneAccountGroupName;
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

	public string IndexCaption => AccountStrings.IndexCaption;

	public uint? Index => this.Account.ZcashAccount.HDDerivation?.AccountIndex;

	public bool IsBirthdayHeightVisible => this.Account.ZcashAccount.BirthdayHeight is not null;

	public string BirthdayHeightCaption => AccountStrings.BirthdayHeightCaption;

	public ulong? BirthdayHeight => this.Account.ZcashAccount.BirthdayHeight;

	public SecurityAmount Balance => this.balance.Value;

	public bool AreKeysRevealed
	{
		get => this.areKeysRevealed;
		set => this.RaiseAndSetIfChanged(ref this.areKeysRevealed, value);
	}

	public string FullViewingKeyCaption => AccountStrings.FullViewingKeyCaption;

	public string? FullViewingKey { get; }

	public string IncomingViewingKeyCaption => AccountStrings.IncomingViewingKeyCaption;

	public string IncomingViewingKey { get; }

	public string RevealKeysCommandCaption => AccountStrings.RevealKeysCommandCaption;
}
