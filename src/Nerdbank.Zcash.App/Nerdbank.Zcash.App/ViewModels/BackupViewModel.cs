// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Microsoft;

namespace Nerdbank.Zcash.App.ViewModels;

/// <summary>
/// Displays the seed phrase, birthday height, # of accounts used, and backup to file button.
/// </summary>
public class BackupViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private readonly HDWallet wallet;
	private bool revealData;
	private string backupFilePassword = string.Empty;
	private bool enableHidingPassword = true;

	[Obsolete("Design-time only", error: true)]
	public BackupViewModel()
		: this(new DesignTimeViewModelServices(), null)
	{
		this.Password = "SomePassword";
	}

	public BackupViewModel(IViewModelServices viewModelServices, HDWallet? wallet)
		: base(viewModelServices)
	{
		wallet ??= viewModelServices.SelectedHDWallet ?? throw new ArgumentNullException(nameof(wallet));
		Requires.Argument(wallet.Zip32.Mnemonic is not null, nameof(wallet), "This HD wallet does not know its own mnemonic.");

		this.wallet = wallet;

		this.BackupCommand = ReactiveCommand.Create(() => { });
		this.RevealCommand = ReactiveCommand.Create(() => { this.IsRevealed = !this.IsRevealed; });
		this.GenerateSecurePasswordCommand = ReactiveCommand.Create(this.GenerateSecurePassword);

		Bip39Mnemonic mnemonic = wallet.Zip32.Mnemonic;
		this.SeedPhrase = mnemonic.SeedPhrase;
		this.SeedPhraseRows = new(BreakupSeedPhraseIntoRows(mnemonic));
		this.Password = mnemonic.Password.ToString();

		this.BirthdayHeight = wallet.BirthdayHeight;
		this.MaxAccountIndex = wallet.MaxAccountIndex;

		this.LinkProperty(nameof(this.EnableHidingPassword), nameof(this.ConcealPasswordChar));
	}

	public string Title => "Backup";

	public ReactiveCommand<Unit, Unit> BackupCommand { get; }

	public string BackupCommandCaption => "Backup to file";

	public string BackupToFileExplanation => "Backing up your wallet will save your seed phrase, contacts and transaction history, allowing you to restore your wallet completely and quickly on another device.";

	public string BackupSeedPhraseExplanation => "Your seed phrase is the key to viewing and spending your Zcash. If you use this instead of the Backup option, copy down your seed phrase and password to a secure location (e.g. on paper, in a safe deposit box).";

	public string BackupFilePasswordCaption => "Protect your backup file with a secure password:";

	public string GenerateSecurePasswordCaption => "Generate secure password";

	public ReactiveCommand<Unit, Unit> GenerateSecurePasswordCommand { get; }

	public string BackupFilePasswordExplanation => "You will need to enter this password when restoring your wallet. If you forget this password, you will not be able to restore your wallet. Anyone with the backup file and the password will be able to spend your Zcash and view all your transactions.";

	public string BackupFilePassword
	{
		get => this.backupFilePassword;
		set
		{
			if (this.backupFilePassword != value)
			{
				this.EnableHidingPassword = true;
				this.RaiseAndSetIfChanged(ref this.backupFilePassword, value);
			}
		}
	}

	public string BackupFilePasswordWatermark => "Password";

	public bool EnableHidingPassword
	{
		get => this.enableHidingPassword;
		set => this.RaiseAndSetIfChanged(ref this.enableHidingPassword, value);
	}

	public string ConcealPasswordChar => this.EnableHidingPassword ? "●" : string.Empty;

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

	public ulong? BirthdayHeight { get; }

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
		get => this.wallet.IsSeedPhraseBackedUp;
		set
		{
			if (this.wallet.IsSeedPhraseBackedUp != value)
			{
				this.wallet.IsSeedPhraseBackedUp = value;
				this.RaisePropertyChanged();
			}
		}
	}

	public void GenerateSecurePassword()
	{
		this.BackupFilePassword = GeneratePassword(Zip32HDWallet.MinimumEntropyLengthInBits);
		this.EnableHidingPassword = false;
	}

	private static string GeneratePassword(int entropyLengthInBits)
	{
		// The character sequence intentionally excludes commonly misread characters.
		const string validChars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRTUVWXYZ234679!@#$%^&*()_+-=[];':\",.<>?~";
		StringBuilder password = new(entropyLengthInBits / 8);

		while (password.Length < entropyLengthInBits / 8)
		{
			password.Append(validChars[RandomNumberGenerator.GetInt32(validChars.Length)]);
		}

		return password.ToString();
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
