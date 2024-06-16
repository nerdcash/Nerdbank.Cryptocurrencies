// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using DynamicData;
using Microsoft.VisualStudio.Threading;

namespace Nerdbank.Zcash.App.ViewModels;

public class CreateNewAccountDetailsViewModel : ViewModelBase
{
	private readonly AsyncLazy<ulong> mainNetChainLength;
	private readonly AsyncLazy<ulong> testNetChainLength;
	private readonly IViewModelServices viewModelServices;
	private readonly ObservableAsPropertyHelper<bool> isHDWalletSelectionVisible;
	private readonly ObservableAsPropertyHelper<string?> minimumBirthdayHeightForHDWalletAdvisory;
	private HDWallet? hdwallet;
	private uint index;
	private string name = string.Empty;
	private ZcashNetwork network = ZcashNetwork.MainNet;
	private ulong? birthdayHeight = 0;
	private ulong? maximumBirthdayHeight;

	[Obsolete("Design-time only", error: true)]
	public CreateNewAccountDetailsViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public CreateNewAccountDetailsViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.mainNetChainLength = new(async () => await AppUtilities.GetChainLengthAsync(viewModelServices, ZcashNetwork.MainNet, CancellationToken.None));
		this.testNetChainLength = new(async () => await AppUtilities.GetChainLengthAsync(viewModelServices, ZcashNetwork.TestNet, CancellationToken.None));

		this.HDWallet = viewModelServices.Wallet.HDWallets.FirstOrDefault();
		this.isHDWalletSelectionVisible = viewModelServices.Wallet.HDWallets.AsObservableChangeSet()
			.Select(_ => viewModelServices.Wallet.HDWallets.Count > 1)
			.ToProperty(this, nameof(this.IsHDWalletSelectionVisible));
		this.minimumBirthdayHeightForHDWalletAdvisory = this.WhenAnyValue(
			vm => vm.HDWallet,
			vm => vm.Network,
			this.CreateBirthdayHeightRecommendation)
			.ToProperty(this, nameof(this.MinimumBirthdayHeightForHDWalletAdvisory), initialValue: this.CreateBirthdayHeightRecommendation(this.HDWallet, this.Network));

		this.CreateAccountCommand = ReactiveCommand.Create(this.CreateAccount, this.IsValid);
		this.SetBirthdayHeightToTipCommand = ReactiveCommand.CreateFromTask(this.SetBirthdayHeightToTipAsync);

		this.LinkProperty(nameof(this.Network), nameof(this.MinimumBirthdayHeight));

		this.UpdateBirthdayHeightAsync().Forget();
		this.UpdateMaxBirthdayHeightAsync().Forget();
		this.UpdateIndex();
		this.Name = this.GetSuggestedAccountName();
	}

	public bool IsHDWalletSelectionVisible => this.isHDWalletSelectionVisible.Value;

	public string HDWalletCaption => CreateNewAccountDetailsStrings.HDWalletCaption;

	[Required]
	public HDWallet? HDWallet
	{
		get => this.hdwallet;
		set
		{
			if (this.hdwallet != value)
			{
				using ConsiderUpdatingName nameUpdater = new(this);
				this.hdwallet = value;
				this.RaisePropertyChanged();
				this.UpdateIndex();
			}
		}
	}

	public ReadOnlyObservableCollection<HDWallet> HDWallets => this.viewModelServices.Wallet.HDWallets;

	public string NetworkCaption => CreateNewAccountDetailsStrings.NetworkCaption;

	public ZcashNetwork Network
	{
		get => this.network;
		set
		{
			if (this.network != value)
			{
				using ConsiderUpdatingName nameUpdater = new(this);
				bool updateBirthdayHeight = this.BirthdayHeight == 0 || (this.LazyHeight.IsValueFactoryCompleted && this.LazyHeight.GetValue() == this.BirthdayHeight);

				this.network = value;

				this.RaisePropertyChanged();
				this.RaisePropertyChanged(nameof(this.IsMainNetChecked));
				this.RaisePropertyChanged(nameof(this.IsTestNetChecked));
				this.UpdateIndex();
				this.UpdateMaxBirthdayHeightAsync().Forget();

				if (updateBirthdayHeight)
				{
					this.UpdateBirthdayHeightAsync().Forget();
				}
			}
		}
	}

	public bool IsMainNetChecked
	{
		get => this.Network == ZcashNetwork.MainNet;
		set => this.Network = value ? ZcashNetwork.MainNet : ZcashNetwork.TestNet;
	}

	public bool IsTestNetChecked
	{
		get => this.Network == ZcashNetwork.TestNet;
		set => this.Network = value ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
	}

	public string MainNetCaption => CreateNewAccountDetailsStrings.FormatMainNetCaption(ZcashNetwork.MainNet.AsSecurity().TickerSymbol);

	public string TestNetCaption => CreateNewAccountDetailsStrings.FormatTestNetCaption(ZcashNetwork.TestNet.AsSecurity().TickerSymbol);

	public string TestNetExplanation => CreateNewAccountDetailsStrings.TestNetExplanation;

	public string IndexCaption => CreateNewAccountDetailsStrings.IndexCaption;

	public uint Index
	{
		get => this.index;
		set
		{
			using ConsiderUpdatingName nameUpdater = new(this);
			this.RaiseAndSetIfChanged(ref this.index, value);

			if (this.HDWallet is not null && this.viewModelServices.Wallet.GetAccountsUnder(this.HDWallet, this.Network).Any(a => a.ZcashAccount.HDDerivation?.AccountIndex == value))
			{
				this.RecordValidationError("An account with this index already exists in this wallet.", nameof(this.Index));
			}
			else
			{
				this.RecordValidationError(null, nameof(this.Index));
			}
		}
	}

	public uint MaximumIndex => uint.MaxValue;

	public string IndexExplanation => CreateNewAccountDetailsStrings.IndexExplanation;

	public string NameCaption => CreateNewAccountDetailsStrings.NameCaption;

	[Required]
	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	public string BirthdayHeightCaption => CreateNewAccountDetailsStrings.BirthdayHeightCaption;

	[Required]
	public ulong? BirthdayHeight
	{
		get => this.birthdayHeight;
		set => this.RaiseAndSetIfChanged(ref this.birthdayHeight, value);
	}

	public ulong MinimumBirthdayHeight => this.NetworkParameters.SaplingActivationHeight;

	public ulong? MaximumBirthdayHeight
	{
		get => this.maximumBirthdayHeight;
		set => this.RaiseAndSetIfChanged(ref this.maximumBirthdayHeight, value);
	}

	public string? MinimumBirthdayHeightForHDWalletAdvisory => this.minimumBirthdayHeightForHDWalletAdvisory.Value;

	public string CreateAccountCommandCaption => CreateNewAccountDetailsStrings.CreateAccountCommandCaption;

	public ReactiveCommand<Unit, Account> CreateAccountCommand { get; }

	public string SetBirthdayHeightToTipCommandCaption => CreateNewAccountDetailsStrings.SetBirthdayHeightToTipCommandCaption;

	public ReactiveCommand<Unit, Unit> SetBirthdayHeightToTipCommand { get; }

	private ZcashNetworkParameters NetworkParameters => ZcashNetworkParameters.GetParameters(this.Network);

	private AsyncLazy<ulong> LazyHeight => this.Network switch
	{
		ZcashNetwork.MainNet => this.mainNetChainLength,
		ZcashNetwork.TestNet => this.testNetChainLength,
		_ => throw new NotSupportedException(),
	};

	public Account CreateAccount()
	{
		Verify.Operation(!this.HasAnyErrors, "Fix validation errors first.");
		Assumes.NotNull(this.HDWallet);

		Account account = new Account(new ZcashAccount(this.HDWallet.GetZip32HDWalletByNetwork(this.Network), this.Index))
		{
			Name = this.Name,
			ZcashAccount =
			{
				BirthdayHeight = this.BirthdayHeight,
			},
		};

		this.viewModelServices.Wallet.Add(account);
		this.viewModelServices.NavigateBack(this);
		return account;
	}

	public async Task SetBirthdayHeightToTipAsync(CancellationToken cancellationToken)
	{
		this.BirthdayHeight = await this.LazyHeight.GetValueAsync(cancellationToken);
	}

	private async ValueTask UpdateMaxBirthdayHeightAsync()
	{
		this.MaximumBirthdayHeight = await this.LazyHeight.GetValueAsync(CancellationToken.None);
	}

	private async ValueTask UpdateBirthdayHeightAsync()
	{
		ulong? originalBirthdayHeight = this.BirthdayHeight;

		ulong currentHeight = await this.LazyHeight.GetValueAsync();

		// Don't set the property if someone set it while we were asynchronously yielding.
		if (this.BirthdayHeight == originalBirthdayHeight)
		{
			this.BirthdayHeight = currentHeight;
		}
	}

	private void UpdateIndex()
	{
		if (this.HDWallet is not null)
		{
			this.Index = (this.viewModelServices.Wallet.GetMaxAccountIndex(this.HDWallet, this.Network) + 1) ?? 0;
		}
	}

	private string GetSuggestedAccountName()
	{
		return $"{this.HDWallet?.Name} {this.Index} ({this.Network.AsSecurity().TickerSymbol})";
	}

	private string? CreateBirthdayHeightRecommendation(HDWallet? hd, ZcashNetwork network)
	{
		if (hd is null)
		{
			return null;
		}

		Account[] accounts = this.viewModelServices.Wallet.GetAccountsUnder(hd, network).ToArray();
		if (accounts.Length == 0)
		{
			return null;
		}

		return $"If you are recovering a previously created account, consider using {accounts.Min(a => a.ZcashAccount.BirthdayHeight)} as the birthday height.";
	}

	private struct ConsiderUpdatingName : IDisposable
	{
		private readonly CreateNewAccountDetailsViewModel owner;
		private bool shouldChange;

		internal ConsiderUpdatingName(CreateNewAccountDetailsViewModel owner)
		{
			this.owner = owner;
			this.shouldChange = owner.GetSuggestedAccountName() == owner.Name;
		}

		public void Dispose()
		{
			if (this.shouldChange)
			{
				this.owner.Name = this.owner.GetSuggestedAccountName();
			}
		}
	}
}
