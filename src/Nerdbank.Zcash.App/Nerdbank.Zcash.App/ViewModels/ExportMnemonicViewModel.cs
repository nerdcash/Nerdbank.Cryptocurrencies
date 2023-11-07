// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App.ViewModels;

public class ExportMnemonicViewModel : ExportAccountViewModelBase
{
	private readonly IViewModelServices viewModelServices;
	private readonly ZcashMnemonic mnemonic;

	[Obsolete("For design-time use only.", error: true)]
	public ExportMnemonicViewModel()
		: this(new DesignTimeViewModelServices(), new ZcashMnemonic())
	{
		this.Password = "SomePassword";
	}

	public ExportMnemonicViewModel(IViewModelServices viewModelServices, ZcashMnemonic mnemonic)
		: base(viewModelServices, mnemonic.BirthdayHeight)
	{
		this.viewModelServices = viewModelServices;
		this.mnemonic = mnemonic;

		this.SeedPhrase = mnemonic.Bip39.SeedPhrase;
		this.SeedPhraseRows = new(BreakupSeedPhraseIntoRows(mnemonic.Bip39));
		this.Password = mnemonic.Bip39.Password.ToString();

		this.MaxAccountIndex = viewModelServices.Wallet.GetMaxAccountIndex(mnemonic) ?? 0;
	}

	public string BackupSeedPhraseExplanation => "Your seed phrase is the key to viewing and spending your Zcash. If you use this instead of the Backup option, copy down your seed phrase and password to a secure location (e.g. on paper, in a safe deposit box).";

	public string SeedPhrase { get; init; } = string.Empty;

	public string SeedPhraseCaption => "Seed phrase";

	public ReadOnlyCollection<SeedPhraseRow> SeedPhraseRows { get; }

	public string PasswordCaption => "Password (if any)";

	public string Password { get; init; } = string.Empty;

	public string MaxAccountIndexCaption => "Max account index";

	public uint MaxAccountIndex { get; init; }

	public string IsSeedPhraseBackedUpCaption => "I have copied down my seed phrase (and password)";

	public bool IsSeedPhraseBackedUp
	{
		get => this.mnemonic.IsBackedUp;
		set
		{
			if (this.mnemonic.IsBackedUp != value)
			{
				this.mnemonic.IsBackedUp = value;
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
