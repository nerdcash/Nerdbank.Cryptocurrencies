// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

/// <summary>
/// Displays the seed phrase, birthday height, # of accounts used, and backup to file button.
/// </summary>
public class BackupViewModel : ViewModelBase
{
	private bool revealData;
	private bool seedPhraseBackedUp;

	[Obsolete("Design-time only", error: true)]
	public BackupViewModel()
		: this(Bip39Mnemonic.Create(256))
	{
		this.BirthdayHeight = 23_456_789;
		this.Password = "SomePassword";
	}

	public BackupViewModel(Bip39Mnemonic mnemonic)
	{
		this.BackupCommand = ReactiveCommand.Create(() => { });
		this.RevealCommand = ReactiveCommand.Create(() => { this.IsRevealed = !this.IsRevealed; });

		this.SeedPhrase = mnemonic.SeedPhrase;
		this.SeedPhraseRows = new(BreakupSeedPhraseIntoRows(mnemonic));
		this.Password = mnemonic.Password.ToString();
	}

	public string Title => "Backup";

	public ReactiveCommand<Unit, Unit> BackupCommand { get; }

	public string BackupCommandCaption => "Backup to file";

	public string BackupToFileExplanation => "While the information presented above is sufficient to recover your funds in another wallet, you are strongly encouraged to backup the auxiliary data from this wallet into a file. The backup file will NOT include your seed phrase.";

	public string BackupSeedPhraseImportant => "It is vital that you copy down your seed phrase and password to a secure location (e.g. on paper, in a safe deposit box). You will need this to recover your funds or recover from your backup file.";

	public ReactiveCommand<Unit, Unit> RevealCommand { get; }

	public string RevealCommandCaption => "Reveal seed phrase";

	public List<string> RevealCautions => new()
	{
		"🕵🏼 Revealing your seed phrase on a screen may compromise your security. If you are in a public place, you should not reveal your seed phrase.",
		"📷 If you take a screenshot of your seed phrase, your backup may be viewed by other apps or uploaded to the cloud. You can make a safe backup with physical paper and pen and storing it in a secure location.",
	};

	public bool IsRevealed
	{
		get => this.revealData;
		set => this.RaiseAndSetIfChanged(ref this.revealData, value);
	}

	public string BirthdayHeightCaption => "Birthday height";

	public required uint BirthdayHeight { get; init; }

	public string SeedPhrase { get; init; } = string.Empty;

	public string SeedPhraseCaption => "Seed phrase";

	public ReadOnlyCollection<SeedPhraseRow> SeedPhraseRows { get; }

	public string PasswordCaption => "Password (if any)";

	public string Password { get; init; } = string.Empty;

	public string MaxAccountIndexCaption => "Max account index";

	public uint MaxAccountIndex { get; init; } = 1;

	public string SeedPhraseBackedUpCaption => "I have copied down my seed phrase (and password)";

	public bool SeedPhraseBackedUp
	{
		get => this.seedPhraseBackedUp;
		set => this.RaiseAndSetIfChanged(ref this.seedPhraseBackedUp, value);
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

	public class SeedPhraseRow : ViewModelBase
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
