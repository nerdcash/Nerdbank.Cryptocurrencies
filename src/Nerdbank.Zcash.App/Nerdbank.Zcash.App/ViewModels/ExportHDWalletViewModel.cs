// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App.ViewModels;

public class ExportHDWalletViewModel : ExportAccountViewModelBase
{
	private readonly IViewModelServices viewModelServices;
	private readonly HDWallet hd;

	[Obsolete("For design-time use only.", error: true)]
	public ExportHDWalletViewModel()
		: this(new DesignTimeViewModelServices(), HDWallet.DesignTimeWallet)
	{
		this.Password = "SomePassword";
	}

	public ExportHDWalletViewModel(IViewModelServices viewModelServices, HDWallet hd)
		: base(viewModelServices, hd.BirthdayHeight)
	{
		this.viewModelServices = viewModelServices;
		this.hd = hd;

		this.SeedPhraseRows = new(BreakupSeedPhraseIntoRows(hd.Mnemonic));
		this.Password = hd.Mnemonic.Password.ToString();

		this.MainNetMaxAccountIndex = viewModelServices.Wallet.GetMaxAccountIndex(hd, ZcashNetwork.MainNet);
		this.TestNetMaxAccountIndex = viewModelServices.Wallet.GetMaxAccountIndex(hd, ZcashNetwork.TestNet);
	}

	public string BackupSeedPhraseExplanation => "Your seed phrase is the key to viewing and spending your Zcash. If you use this instead of the Backup option, copy down your seed phrase and password to a secure location (e.g. on paper, in a safe deposit box).";

	public string SeedPhrase => this.hd.Mnemonic.SeedPhrase;

	public string SeedPhraseCaption => "Seed phrase";

	public ReadOnlyCollection<SeedPhraseRow> SeedPhraseRows { get; }

	public string PasswordCaption => "Password (if any)";

	public string Password { get; init; } = string.Empty;

	public string MainNetMaxAccountIndexCaption => "Max account index (MainNet)";

	public string TestNetMaxAccountIndexCaption => "Max account index (TestNet)";

	public uint? MainNetMaxAccountIndex { get; init; }

	public uint? TestNetMaxAccountIndex { get; init; }

	public string IsSeedPhraseBackedUpCaption => "I have copied down my seed phrase (and password)";

	public bool IsSeedPhraseBackedUp
	{
		get => this.hd.IsBackedUp;
		set
		{
			if (this.hd.IsBackedUp != value)
			{
				this.hd.IsBackedUp = value;
				this.RaisePropertyChanged();
			}
		}
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
