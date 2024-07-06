// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Input;

namespace Nerdbank.Zcash.App.ViewModels;

public class SyncProgressData : ProgressData
{
	private readonly ObservableAsPropertyHelper<bool> progressBarVisibleOnOtherScreens;
	private readonly ObservableAsPropertyHelper<bool> isStatusCaughtUp;
	private readonly ObservableAsPropertyHelper<bool> isStatusInProgress;
	private readonly ObservableAsPropertyHelper<bool> isStatusDisconnected;
	private readonly ObservableAsPropertyHelper<bool> isStatusDisconnectedWithError;
	private readonly ObservableAsPropertyHelper<string> toolTip;
	private uint? lastFullyScannedBlock;
	private uint? tipHeight;
	private string? lastError;
	private Status currentStatus;

	[Obsolete("For design-time use only.", error: true)]
	public SyncProgressData()
		: this(new DesignTimeViewModelServices(), null)
	{
		this.IsInProgress = true;
		this.From = 2_100_803;
		this.To = 2_200_350;
		this.Current = 2_180_100;
		this.LastFullyScannedBlock = 800_000;
		this.TipHeight = 2_200_350;
		this.Network = ZcashNetwork.TestNet;
	}

	public SyncProgressData(IViewModelServices viewModelServices, WalletSyncManager.ITracker? syncTracker)
	{
		this.Network = syncTracker?.Network;
		this.Tracker = syncTracker;

		// Avoid activating progress bars if we're only one block behind, which happens a lot.
		this.progressBarVisibleOnOtherScreens = this.WhenAnyValue(
			x => x.LastFullyScannedBlock,
			x => x.TipHeight,
			x => x.Current,
			x => x.To,
			(lastFullyScannedBlock, tipHeight, current, to) => lastFullyScannedBlock is null || lastFullyScannedBlock < tipHeight - 1 || to == 0)
			.ToProperty(this, nameof(this.ProgressBarVisibleOnOtherScreens));

		this.isStatusCaughtUp = CurrentStatusBoolCheck(Status.CaughtUp, nameof(this.IsStatusCaughtUp));
		this.isStatusInProgress = CurrentStatusBoolCheck(Status.InProgress, nameof(this.IsStatusInProgress));
		this.isStatusDisconnected = CurrentStatusBoolCheck(Status.Disconnected, nameof(this.IsStatusDisconnected));
		this.isStatusDisconnectedWithError = CurrentStatusBoolCheck(Status.DisconnectedWithError, nameof(this.IsStatusDisconnectedWithError));
		ObservableAsPropertyHelper<bool> CurrentStatusBoolCheck(Status desiredState, string propertyName)
			=> this.WhenAnyValue(x => x.CurrentStatus, status => status == desiredState).ToProperty(this, propertyName);

		IObservable<bool> canRetry = this.WhenAnyValue(x => x.CurrentStatus, status => syncTracker is not null && status == Status.DisconnectedWithError);
		this.RetryCommand = ReactiveCommand.Create(() => syncTracker?.Retry(), canRetry);

		this.ShowDetailsCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new SyncDetailsViewModel(viewModelServices)));

		this.toolTip = this.WhenAnyValue(
			x => x.CurrentStatus,
			x => x.LastError,
			(status, error) => status switch
			{
				Status.DisconnectedWithError => SyncStrings.DisconnectedWithError,
				Status.Disconnected => SyncStrings.Disconnected,
				Status.InProgress => SyncStrings.InProgress,
				Status.CaughtUp => SyncStrings.CaughtUp,
				_ => throw new NotImplementedException(),
			})
			.ToProperty(this, nameof(this.ToolTip));
	}

	/// <summary>
	/// Describes the current status of the sync operation.
	/// </summary>
	public enum Status
	{
		/// <summary>
		/// Sync is not occurring because the wallet is not connected to the server.
		/// </summary>
		Disconnected,

		/// <summary>
		/// Sync is in progress.
		/// </summary>
		InProgress,

		/// <summary>
		/// The client is connected to the server but not actively scanning because we're caught up,
		/// waiting for another block to be mined.
		/// </summary>
		CaughtUp,

		/// <summary>
		/// Sync is not occurring because of an error.
		/// </summary>
		DisconnectedWithError,
	}

	public WalletSyncManager.ITracker? Tracker { get; }

	public ZcashNetwork? Network { get; }

	public string RetryCommandCaption => SyncStrings.Retry;

	public ICommand RetryCommand { get; }

	public ICommand ShowDetailsCommand { get; }

	public Status CurrentStatus
	{
		get => this.currentStatus;
		set => this.RaiseAndSetIfChanged(ref this.currentStatus, value);
	}

	public bool IsStatusCaughtUp => this.isStatusCaughtUp.Value;

	public bool IsStatusInProgress => this.isStatusInProgress.Value;

	public bool IsStatusDisconnected => this.isStatusDisconnected.Value;

	public bool IsStatusDisconnectedWithError => this.isStatusDisconnectedWithError.Value;

	public string LastFullyScannedBlockCaption => SyncStrings.LastFullyScannedBlockCaption;

	public uint? LastFullyScannedBlock
	{
		get => this.lastFullyScannedBlock;
		set => this.RaiseAndSetIfChanged(ref this.lastFullyScannedBlock, value);
	}

	public string TipHeightCaption => SyncStrings.TipHeightCaption;

	public uint? TipHeight
	{
		get => this.tipHeight;
		set => this.RaiseAndSetIfChanged(ref this.tipHeight, value);
	}

	public string? LastError
	{
		get => this.lastError;
		set => this.RaiseAndSetIfChanged(ref this.lastError, value);
	}

	public string ToolTip => this.toolTip.Value;

	public bool ProgressBarVisibleOnOtherScreens => this.progressBarVisibleOnOtherScreens.Value;

	public override string Caption => SyncStrings.ProgressBarCaption;

	public override uint? VisiblyApparentStepSize => 10_000; // A step size that will likely be crossed within a couple minutes on any device.

	internal static SyncProgressData Blend(IViewModelServices viewModelServices, ImmutableArray<SyncProgressData> progressObjects) => new BlendedSyncProgressData(viewModelServices, progressObjects);

	internal void Apply(LightWalletClient.SyncProgress v)
	{
		this.IsInProgress = v.LastFullyScannedBlock != v.TipHeight;
		this.To = v.TotalSteps;
		this.Current = v.CurrentStep;
		this.TipHeight = v.TipHeight;
		this.LastFullyScannedBlock = v.LastFullyScannedBlock;
		this.LastError = v.LastError;

		if (v.LastFullyScannedBlock == v.TipHeight)
		{
			this.CurrentStatus = Status.CaughtUp;
		}
		else
		{
			this.CurrentStatus = Status.InProgress;
		}
	}

	internal void ReportDisconnect(string? errorMessage)
	{
		this.CurrentStatus = errorMessage is null ? Status.Disconnected : Status.DisconnectedWithError;
		this.LastError = errorMessage;
	}

	private class BlendedSyncProgressData : SyncProgressData, IDisposable
	{
		private readonly ImmutableArray<SyncProgressData> progressObjects;

		internal BlendedSyncProgressData(IViewModelServices viewModelServices, ImmutableArray<SyncProgressData> progressObjects)
			: base(viewModelServices, null)
		{
			this.progressObjects = progressObjects;
			foreach (SyncProgressData progress in progressObjects)
			{
				progress.PropertyChanged += this.Progress_PropertyChanged;
			}
		}

		public void Dispose()
		{
			foreach (SyncProgressData progress in this.progressObjects)
			{
				progress.PropertyChanged -= this.Progress_PropertyChanged;
			}
		}

		private void Progress_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			this.To = (ulong)this.progressObjects.Sum(x => (long)(x.To - x.From));
			this.Current = (ulong)this.progressObjects.Sum(x => (long)(x.Current - x.From));
			this.CurrentStatus = this.progressObjects.Min(x => x.CurrentStatus);
			this.LastError = this.progressObjects.FirstOrDefault(o => o.LastError is not null)?.LastError;
			this.IsInProgress = this.progressObjects.Any(x => x.IsInProgress);
		}
	}
}
