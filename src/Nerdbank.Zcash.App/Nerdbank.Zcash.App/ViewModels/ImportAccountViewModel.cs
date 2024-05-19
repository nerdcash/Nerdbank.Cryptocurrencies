// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Nerdbank.Zcash.App.ViewModels;

public class ImportAccountViewModel : ViewModelBase, IHasTitle, INotifyDataErrorInfo
{
	private readonly IViewModelServices viewModelServices;
	private string key = string.Empty;
	private string seedPassword = string.Empty;
	private bool isTestNet;
	private ulong? birthdayHeight;
	private Bip39Mnemonic? mnemonic;
	private bool inputIsValidKey;
	private bool isSeed;
	private bool isPasswordVisible;
	private string name = string.Empty;

	[Obsolete("Design-time only", error: true)]
	public ImportAccountViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public ImportAccountViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;
		this.ImportCommand = ReactiveCommand.Create(this.Import, this.IsValid);

		this.LinkProperty(nameof(this.IsSeed), nameof(this.IsTestNetVisible));
		this.LinkProperty(nameof(this.SeedPassword), nameof(this.SeedPasswordHasWhitespace));
		this.LinkProperty(nameof(this.IsTestNet), nameof(this.MinimumBirthdayHeight));

		this.BirthdayHeight = this.NetworkParameters.SaplingActivationHeight;

		if (viewModelServices.Wallet.IsEmpty)
		{
			this.Name = ImportAccountStrings.DefaultNameForFirstImportedAccount;
		}
	}

	public string Title => ImportAccountStrings.Title;

	public string NameCaption => ImportAccountStrings.NameCaption;

	[Required]
	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	[Required]
	public string Key
	{
		get => this.key;
		set
		{
			if (this.key != value)
			{
				this.key = value;
				this.OnKeyChanged();
				this.RaisePropertyChanged();
			}
		}
	}

	public string KeyCaption => ImportAccountStrings.KeyCaption;

	public string SeedPasswordCaption => ImportAccountStrings.SeedPasswordCaption;

	public string SeedPassword
	{
		get => this.seedPassword;
		set
		{
			if (this.seedPassword != value)
			{
				this.seedPassword = value;
				this.OnKeyChanged();
				this.RaisePropertyChanged();
			}
		}
	}

	public bool SeedPasswordHasWhitespace => this.SeedPassword.Any(char.IsWhiteSpace);

	public string SeedPasswordWhitespaceWarning => ImportAccountStrings.SeedPasswordWhitespaceWarning;

	public bool IsSeed
	{
		get => this.isSeed;
		private set => this.RaiseAndSetIfChanged(ref this.isSeed, value);
	}

	public bool IsPasswordVisible
	{
		get => this.isPasswordVisible;
		set => this.RaiseAndSetIfChanged(ref this.isPasswordVisible, value);
	}

	public bool IsTestNet
	{
		get => this.isTestNet;
		set
		{
			bool updateBirthdayHeight = this.BirthdayHeight == this.NetworkParameters.SaplingActivationHeight;

			this.RaiseAndSetIfChanged(ref this.isTestNet, value);

			if (updateBirthdayHeight)
			{
				this.BirthdayHeight = this.NetworkParameters.SaplingActivationHeight;
			}
		}
	}

	public string IsTestNetCaption => ImportAccountStrings.IsTestNetCaption;

	public bool IsTestNetVisible => this.IsSeed;

	[Required]
	public ulong? BirthdayHeight
	{
		get => this.birthdayHeight;
		set => this.RaiseAndSetIfChanged(ref this.birthdayHeight, value);
	}

	public string BirthdayHeightCaption => ImportAccountStrings.BirthdayHeightCaption;

	public ulong MinimumBirthdayHeight => this.NetworkParameters.SaplingActivationHeight;

	public string ImportCommandCaption => ImportAccountStrings.ImportCommandCaption;

	public ReactiveCommand<Unit, Account?> ImportCommand { get; }

	private ZcashNetworkParameters NetworkParameters => this.IsTestNet ? ZcashNetworkParameters.TestNet : ZcashNetworkParameters.MainNet;

	public Account? Import()
	{
		Account? account = null;
		if (this.TryImportAccount(out ZcashAccount? zcashAccount))
		{
			account = new Account(zcashAccount)
			{
				Name = this.Name,
			};

			this.viewModelServices.Wallet.Add(account);
		}

		return account;
	}

	private void OnKeyChanged()
	{
		this.TryInitializeMnemonic();

		this.IsSeed = this.mnemonic is not null;
		this.IsPasswordVisible = this.mnemonic?.Password.Length == 0 || this.SeedPassword.Length > 0;

		this.inputIsValidKey = this.mnemonic is null && ZcashAccount.TryImportAccount(this.Key, out _);
		bool isValidInput = this.mnemonic is not null || this.inputIsValidKey;

		this.RecordValidationError(this.Key.Length == 0 || isValidInput ? null : ImportAccountStrings.BadOrUnsupportedImportKey, nameof(this.Key));
	}

	private bool TryInitializeMnemonic()
	{
		if (this.IsPasswordVisible && this.SeedPassword is { Length: > 0 })
		{
			// The user has supplied an explicit seed password.
			return Bip39Mnemonic.TryParse(this.Key, this.SeedPassword, out this.mnemonic, out _, out _);
		}
		else
		{
			// The user has *not* supplied an explicit seed password.
			// But the user *may* have provided the password as an extra word in the seed phrase itself.
			return Bip39Mnemonic.TryParse(this.Key, out this.mnemonic, out _, out _);
		}
	}

	private bool TryImportAccount([NotNullWhen(true)] out ZcashAccount? account)
	{
		this.TryInitializeMnemonic();

		if (this.mnemonic is not null)
		{
			Zip32HDWallet zip32 = new(this.mnemonic, this.IsTestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet);
			account = new ZcashAccount(zip32, 0);
		}
		else
		{
			ZcashAccount.TryImportAccount(this.Key, out account);
		}

		if (account is not null)
		{
			account.BirthdayHeight = this.BirthdayHeight;
		}

		return account is not null;
	}
}
