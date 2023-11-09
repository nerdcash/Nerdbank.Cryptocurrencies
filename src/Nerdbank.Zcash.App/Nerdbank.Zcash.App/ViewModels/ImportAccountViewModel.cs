// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Nerdbank.Zcash.App.ViewModels;

public class ImportAccountViewModel : ViewModelBase, IHasTitle, INotifyDataErrorInfo
{
	private readonly Dictionary<string, ValidationResult[]> errors = new(StringComparer.Ordinal);
	private readonly ObservableBox<bool> importCommandEnabled = new(false);
	private readonly IViewModelServices viewModelServices;
	private string key = string.Empty;
	private string seedPassword = string.Empty;
	private bool isTestNet;
	private ulong birthdayHeight = AppUtilities.SaplingActivationHeight;
	private Bip39Mnemonic? mnemonic;
	private bool inputIsValidKey;
	private bool isSeed;
	private bool isPasswordVisible;
	private string name = string.Empty;
	private bool hasErrors;

	[Obsolete("Design-time only", error: true)]
	public ImportAccountViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public ImportAccountViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;
		this.ImportCommand = ReactiveCommand.Create(this.Import, this.importCommandEnabled);

		this.LinkProperty(nameof(this.IsSeed), nameof(this.IsTestNetVisible));
		this.LinkProperty(nameof(this.SeedPassword), nameof(this.SeedPasswordHasWhitespace));

		if (viewModelServices.Wallet.IsEmpty)
		{
			this.Name = Strings.DefaultNameForFirstImportedAccount;
		}
	}

	public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

	public string Title => "Import Account";

	public string NameCaption => "Account name";

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

	public string KeyCaption => "Enter the seed, private key, full or incoming viewing key for the account you wish to import.";

	public string SeedPasswordCaption => "Seed password (if applicable)";

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

	public string SeedPasswordWhitespaceWarning => "The password includes whitespace. This is not recommended.";

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
		set => this.RaiseAndSetIfChanged(ref this.isTestNet, value);
	}

	public string IsTestNetCaption => "This is a testnet account";

	public bool IsTestNetVisible => this.IsSeed;

	public ulong BirthdayHeight
	{
		get => this.birthdayHeight;
		set => this.RaiseAndSetIfChanged(ref this.birthdayHeight, value);
	}

	public string BirthdayHeightCaption => "Birthday height";

	public ulong MinimumBirthdayHeight => AppUtilities.SaplingActivationHeight;

	public string ImportCommandCaption => "Import";

	public ReactiveCommand<Unit, Account?> ImportCommand { get; }

	public bool HasErrors
	{
		get => this.hasErrors;
		private set => this.RaiseAndSetIfChanged(ref this.hasErrors, value);
	}

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

	public IEnumerable GetErrors(string? propertyName)
	{
		if (propertyName is not null && this.errors.TryGetValue(propertyName, out ValidationResult[]? results))
		{
			return results;
		}

		return Enumerable.Empty<ValidationResult>();
	}

	protected virtual void OnErrorsChanged(DataErrorsChangedEventArgs e) => this.ErrorsChanged?.Invoke(this, e);

	private void RecordValidationError(string? message, [CallerMemberName] string? propertyName = null)
	{
		Requires.NotNull(propertyName!);
		if (message is null)
		{
			if (this.errors.Remove(propertyName))
			{
				this.OnErrorsChanged(new DataErrorsChangedEventArgs(propertyName));
			}
		}
		else
		{
			this.errors[propertyName] = new ValidationResult[] { new(message) };
			this.OnErrorsChanged(new DataErrorsChangedEventArgs(propertyName));
		}
	}

	private void OnKeyChanged()
	{
		this.TryInitializeMnemonic();

		this.IsSeed = this.mnemonic is not null;
		this.IsPasswordVisible = this.mnemonic?.Password.Length == 0 || this.SeedPassword.Length > 0;

		this.inputIsValidKey = this.mnemonic is null && ZcashAccount.TryImportAccount(this.Key, out _);
		this.importCommandEnabled.Value = this.mnemonic is not null || this.inputIsValidKey;

		this.RecordValidationError(this.Key.Length == 0 || this.importCommandEnabled.Value ? null : Strings.BadOrUnsupportedImportKey, nameof(this.Key));
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
