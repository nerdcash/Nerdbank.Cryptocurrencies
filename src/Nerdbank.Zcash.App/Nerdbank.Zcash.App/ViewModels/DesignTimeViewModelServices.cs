// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

internal class DesignTimeViewModelServices : IViewModelServicesWithWallet
{
	public ZcashWallet Wallet { get; } = new();

	public void NavigateBack(ViewModelBase? ifCurrentViewModel)
	{
	}

	public void NavigateTo(ViewModelBase viewModel)
	{
	}
}
