// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DynamicData.Binding;

namespace Nerdbank.Zcash.App.ViewModels;

public class SyncProgressData : ViewModelBase
{
	private readonly IViewModelServices viewModelServices;
	private Account? account;
	private IDisposable? accountSyncSubscription;

	private bool isSyncInProgress;
	private ulong from;
	private ulong to;
	private ulong current;

	[Obsolete("For design-time use only.", error: true)]
	public SyncProgressData()
		: this(new DesignTimeViewModelServices())
	{
		this.isSyncInProgress = true;
		this.from = 2_100_803;
		this.to = 2_200_350;
		this.current = 2_180_100;
	}

	public SyncProgressData(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;
	}

	public SyncProgressData(ViewModelBaseWithAccountSelector owner)
	{
		this.viewModelServices = owner.ViewModelServices;
		owner.WhenPropertyChanged(vm => vm.SelectedAccount).Subscribe(i => this.Account = i.Value);
	}

	public Account? Account
	{
		get => this.account;
		set
		{
			this.RaiseAndSetIfChanged(ref this.account, value);
			this.SubscribeToSyncUpdates(value);
		}
	}

	public string SyncInProgressCaption => "Sync in progress";

	public bool IsSyncInProgress
	{
		get => this.isSyncInProgress;
		set => this.RaiseAndSetIfChanged(ref this.isSyncInProgress, value);
	}

	public ulong From
	{
		get => this.from;
		set => this.RaiseAndSetIfChanged(ref this.from, value);
	}

	public ulong To
	{
		get => this.to;
		set => this.RaiseAndSetIfChanged(ref this.to, value);
	}

	public ulong Current
	{
		get => this.current;
		set => this.RaiseAndSetIfChanged(ref this.current, value);
	}

	internal void Apply(LightWalletClient.SyncProgress? progress)
	{
		this.IsSyncInProgress = progress?.InProgress is true;
		this.To = progress?.BatchTotal ?? 0;
		this.Current = progress?.BatchNum ?? 0;
	}

	private void SubscribeToSyncUpdates(Account? newAccount)
	{
		this.accountSyncSubscription?.Dispose();
		this.accountSyncSubscription = newAccount?.WhenAnyValue(vm => vm.SyncProgress).Subscribe(this.Apply);
	}
}
