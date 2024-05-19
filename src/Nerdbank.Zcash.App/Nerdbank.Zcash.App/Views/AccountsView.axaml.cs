// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using DynamicData.Binding;

namespace Nerdbank.Zcash.App.Views;

public partial class AccountsView : ReactiveUserControl<AccountsViewModel>
{
	public AccountsView()
	{
		this.InitializeComponent();
	}

	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);

		this.ArrangeForGroupingAccounts();
	}

	private void ArrangeForGroupingAccounts()
	{
		DataGridCollectionView view = new(this.AccountsGrid.ItemsSource);
		view.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(AccountViewModel.Name), ListSortDirection.Ascending));
		this.AccountsGrid.ItemsSource = view;

		UpdateGroupDescriptions();
		this.ViewModel!.WhenAnyPropertyChanged(nameof(this.ViewModel.GroupAccountsByHDWallets)).Subscribe(vm => UpdateGroupDescriptions());

		void UpdateGroupDescriptions()
		{
			if (this.ViewModel?.GroupAccountsByHDWallets is true)
			{
				view.GroupDescriptions.Add(new DataGridPathGroupDescription($"{nameof(AccountViewModel.GroupName)}"));
			}
			else
			{
				view.GroupDescriptions.Clear();
			}
		}
	}
}
