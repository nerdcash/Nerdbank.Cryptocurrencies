// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App.ViewModels;

public class SyncDetailsViewModel : ViewModelBase, IHasTitle
{
	[Obsolete("For design-time use only.", error: true)]
	public SyncDetailsViewModel()
	{
		this.SyncDetails = new([new SyncProgressData(), new SyncProgressData()]);
	}

	public SyncDetailsViewModel(IViewModelServices viewModelServices)
	{
		this.SyncDetails = viewModelServices.App.WalletSyncManager?.ProgressDetails ?? new([]);
	}

	public string Title => SyncStrings.SyncDetailsTitle;

	public ReadOnlyObservableCollection<SyncProgressData> SyncDetails { get; }
}
