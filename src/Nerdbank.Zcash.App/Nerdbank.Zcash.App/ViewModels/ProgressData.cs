// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class ProgressData : ViewModelBase
{
	private bool isInProgress;
	private ulong from;
	private ulong to;
	private ulong current;

	public virtual string Caption => "In progress";

	public bool IsInProgress
	{
		get => this.isInProgress;
		set => this.RaiseAndSetIfChanged(ref this.isInProgress, value);
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

	public void Clear()
	{
		this.IsInProgress = false;
		this.Current = 0;
		this.From = 0;
		this.To = 0;
	}
}
