// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DynamicData.Binding;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainMobileViewModel : MainViewModel
{
	private static readonly string AppTitle = Strings.AppTitle;

	[Obsolete("Design-time only", error: true)]
	public MainMobileViewModel()
		: this(new App())
	{
	}

	public MainMobileViewModel(App app)
		: base(app)
	{
	}

	private void WatchSendProgressInAllAccounts()
	{
		// Always present all the sends across all accounts in the app-wide progress indicator.
		this.SendProgress.SubscribeAndMerge(this.Wallet.Accounts.Select(a => a.SendProgress));
		this.Wallet.Accounts.ObserveCollectionChanges().Subscribe(
			_ => this.SendProgress.SubscribeAndMerge(this.Wallet.Accounts.Select(a => a.SendProgress)));
	}
}
