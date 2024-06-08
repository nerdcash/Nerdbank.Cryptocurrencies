// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Nerdbank.Zcash.App.ViewModels;

public class ProgressData : ViewModelBase
{
	private ImmutableArray<ProgressData> subscribed = ImmutableArray<ProgressData>.Empty;
	private bool isInProgress;
	private ulong from;
	private ulong to;
	private ulong current;

	public ProgressData()
	{
		this.LinkProperty(nameof(this.From), nameof(this.ProgressTextFormat));
		this.LinkProperty(nameof(this.To), nameof(this.ProgressTextFormat));
	}

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

	public string ProgressTextFormat => $$"""{1:N{{this.RequiredPrecision}}}%""";

	/// <summary>
	/// Gets the number of steps between <see cref="From"/> and <see cref="To"/> that must be visibly apparent to the user via the numeric % displayed to the user.
	/// </summary>
	/// <remarks>
	/// <para>The lower the number, the higher the % precision that may be displayed.</para>
	/// <para>A <see langword="null" /> value indicates that the % displayed should never include decimal points in the % displayed.</para>
	/// </remarks>
	public virtual uint? VisiblyApparentStepSize => null;

	/// <summary>
	/// Gets the number of decimal places that should be displayed in the % complete.
	/// </summary>
	private uint RequiredPrecision
	{
		get
		{
			checked
			{
				uint precision = 0;
				double length = this.To - this.From;
				if (this.VisiblyApparentStepSize is uint step && step > 0 && length > 0)
				{
					const uint ImplicitPrecision = 2; // % gives us 0-1 with 2 decimal places
					precision = (uint)Math.Max(ImplicitPrecision, Math.Ceiling(Math.Log10(length / step))) - ImplicitPrecision;
				}

				return precision;
			}
		}
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
