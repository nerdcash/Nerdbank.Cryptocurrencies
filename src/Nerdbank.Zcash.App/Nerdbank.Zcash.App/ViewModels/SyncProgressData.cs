// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DynamicData.Binding;

namespace Nerdbank.Zcash.App.ViewModels;

public class SyncProgressData : ViewModelBase
{
	private readonly IViewModelServices viewModelServices;
	private Account? account;

	private bool isSyncInProgress;
	private uint from;
	private uint to;
	private uint current;

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
		set => this.RaiseAndSetIfChanged(ref this.account, value);
	}

	public string SyncInProgressCaption => "Sync in progress";

	public bool IsSyncInProgress
	{
		get => this.isSyncInProgress;
		set => this.RaiseAndSetIfChanged(ref this.isSyncInProgress, value);
	}

	public uint From
	{
		get => this.from;
		set => this.RaiseAndSetIfChanged(ref this.from, value);
	}

	public uint To
	{
		get => this.to;
		set => this.RaiseAndSetIfChanged(ref this.to, value);
	}

	public uint Current
	{
		get => this.current;
		set => this.RaiseAndSetIfChanged(ref this.current, value);
	}
}
