﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App.ViewModels;

public class ExportSeedBasedAccountViewModel : ExportAccountViewModelBase
{
	private readonly IViewModelServices viewModelServices;
	private readonly Bip39Mnemonic mnemonic;
	private readonly HDWallet? hdWallet;

	[Obsolete("For design-time use only.", error: true)]
	public ExportSeedBasedAccountViewModel()
		: this(new DesignTimeViewModelServices(), new Account(new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits)))) { Name = "Design-time account" })
	{
		this.Password = "SomePassword";
	}

	public ExportSeedBasedAccountViewModel(IViewModelServices viewModelServices, Account account)
		: base(viewModelServices, account)
	{
		Bip39Mnemonic? mnemonic = account.ZcashAccount.HDDerivation?.Wallet.Mnemonic;
		Requires.Argument(mnemonic is not null, nameof(account), "Only seed phrase derived accounts are supported here.");
		this.mnemonic = mnemonic;
		if (viewModelServices.Wallet.TryGetHDWallet(account, out HDWallet? hd))
		{
			this.hdWallet = hd;
		}

		this.viewModelServices = viewModelServices;

		this.SeedPhraseRows = new(BreakupSeedPhraseIntoRows(mnemonic));
		this.Password = mnemonic.Password.ToString();

		this.AccountIndex = account.ZcashAccount.HDDerivation!.Value.AccountIndex;

		this.ShowViewingKeysCommand = ReactiveCommand.Create(this.ShowViewingKeys);
	}

	public string BackupSeedPhraseExplanation => ExportSeedBasedAccountStrings.BackupSeedPhraseExplanation;

	public string SeedPhrase => this.mnemonic.SeedPhrase;

	public string SeedPhraseCaption => ExportSeedBasedAccountStrings.SeedPhraseCaption;

	public ReadOnlyCollection<SeedPhraseRow> SeedPhraseRows { get; }

	public string PasswordCaption => ExportSeedBasedAccountStrings.PasswordCaption;

	public string Password { get; init; } = string.Empty;

	public string AccountIndexCaption => ExportSeedBasedAccountStrings.AccountIndexCaption;

	public uint AccountIndex { get; init; }

	public string IsSeedPhraseBackedUpCaption => ExportSeedBasedAccountStrings.IsSeedPhraseBackedUpCaption;

	public bool IsSeedPhraseBackedUp
	{
		get => this.hdWallet?.IsBackedUp ?? false;
		set
		{
			if (this.hdWallet is not null && this.hdWallet.IsBackedUp != value)
			{
				this.hdWallet.IsBackedUp = value;
				this.RaisePropertyChanged();
			}
		}
	}

	public string ShowViewingKeysExplanation => ExportSeedBasedAccountStrings.ShowViewingKeysExplanation;

	public string ShowViewingKeysCommandCaption => ExportSeedBasedAccountStrings.ShowViewingKeysCommandCaption;

	public ReactiveCommand<Unit, Unit> ShowViewingKeysCommand { get; }

	public void ShowViewingKeys()
	{
		this.viewModelServices.NavigateTo(new ExportLoneAccountViewModel(this.viewModelServices, this.Account));
	}

	private static SeedPhraseRow[] BreakupSeedPhraseIntoRows(Bip39Mnemonic mnemonic)
	{
		string[] words = mnemonic.SeedPhrase.Split(' ');
		SeedPhraseRow[] result = new SeedPhraseRow[words.Length / 3];
		for (int i = 0; i < words.Length; i += 3)
		{
			result[i / 3] = new SeedPhraseRow(i + 1, words[i], words[i + 1], words[i + 2]);
		}

		return result;
	}

	public class SeedPhraseRow : ReactiveObject
	{
		public SeedPhraseRow(int startingIndex, string word1, string word2, string word3)
		{
			this.Word1 = $"{startingIndex++}. {word1}";
			this.Word2 = $"{startingIndex++}. {word2}";
			this.Word3 = $"{startingIndex++}. {word3}";
		}

		public string Word1 { get; }

		public string Word2 { get; }

		public string Word3 { get; }
	}
}