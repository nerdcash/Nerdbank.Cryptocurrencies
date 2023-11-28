// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.ComponentModel;

namespace Nerdbank.Zcash.App.ViewModels;

public class ProgressData : ViewModelBase
{
	private ImmutableArray<ProgressData> subscribed = ImmutableArray<ProgressData>.Empty;
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

	/// <summary>
	/// Reports that any in-progress operation is complete,
	/// ultimately settling to a cleared state.
	/// </summary>
	public void Complete()
	{
		// Consider temporarily moving to 100% and holding before clearing
		// so the user has a sense of completion.
		this.Clear();
	}

	/// <summary>
	/// Resets all properties to their default values and reports that nothing is in progress.
	/// </summary>
	public void Clear()
	{
		this.IsInProgress = false;
		this.Current = 0;
		this.From = 0;
		this.To = 0;

		foreach (ProgressData other in this.subscribed)
		{
			other.PropertyChanged -= this.SubscribedProgress_PropertyChanged;
		}

		this.subscribed = this.subscribed.Clear();
	}

	/// <summary>
	/// Configures this instance to be an aggregate of other instances.
	/// </summary>
	/// <param name="otherProgress">The other progress indicators that should be aggregated into this one.</param>
	/// <remarks>
	/// This method has no impact on the instances given in <paramref name="otherProgress"/>.
	/// This object will remain subscribed until <see cref="Clear"/> is called.
	/// </remarks>
	public void SubscribeAndMerge(IEnumerable<ProgressData> otherProgress)
	{
		this.Clear();
		this.subscribed = otherProgress.ToImmutableArray();
		foreach (ProgressData other in this.subscribed)
		{
			other.PropertyChanged += this.SubscribedProgress_PropertyChanged;
		}

		this.UpdateSelfBySubscribed();
	}

	private void UpdateSelfBySubscribed()
	{
		ulong totalWork = 0;
		ulong workDone = 0;
		bool inProgress = false;

		foreach (ProgressData other in this.subscribed)
		{
			if (other.IsInProgress)
			{
				inProgress = true;
				totalWork += other.To - other.From;
				workDone += other.Current - other.From;
			}
		}

		this.IsInProgress = inProgress;
		this.From = 0;
		this.To = totalWork;
		this.Current = workDone;
	}

	private void SubscribedProgress_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		this.UpdateSelfBySubscribed();
	}
}
