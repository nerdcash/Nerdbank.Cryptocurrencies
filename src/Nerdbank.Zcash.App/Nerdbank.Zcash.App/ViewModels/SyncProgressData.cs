// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DynamicData.Binding;

namespace Nerdbank.Zcash.App.ViewModels;

public class SyncProgressData : ProgressData
{
	private readonly IViewModelServices viewModelServices;
	private Account? account;
	private IDisposable? accountSyncSubscription;

	[Obsolete("For design-time use only.", error: true)]
	public SyncProgressData()
		: this(new DesignTimeViewModelServices())
	{
		this.IsInProgress = true;
		this.From = 2_100_803;
		this.To = 2_200_350;
		this.Current = 2_180_100;
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

	public override string Caption => "Sync in progress";

	public override uint? VisiblyApparentStepSize => 10_000; // A step size that will likely be crossed within a couple minutes on any device.

	internal void Apply(LightWalletClient.SyncProgress? progress)
	{
		this.IsInProgress = progress is not null && progress.LastFullyScannedBlock != progress.TipHeight;
		this.To = progress?.TotalSteps ?? 0;
		this.Current = progress?.CurrentStep ?? 0;
	}

	private void SubscribeToSyncUpdates(Account? newAccount)
	{
		this.accountSyncSubscription?.Dispose();
		this.accountSyncSubscription = newAccount?.WhenAnyValue(vm => vm.SyncProgress).Subscribe(this.Apply);
	}
}
