// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class SyncProgressData : ViewModelBase
{
	private bool isSyncInProgress = true;
	private uint from = 2_100_803;
	private uint to = 2_200_350;
	private uint current = 2_180_100;

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
