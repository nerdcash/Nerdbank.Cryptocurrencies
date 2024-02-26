// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;

namespace Nerdbank.Zcash.App.Views;

public partial class HistoryView : ReactiveUserControl<HistoryViewModel>
{
	public HistoryView()
	{
		this.InitializeComponent();
	}

	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);

		// We set this here instead of in the XAML because in the XAML, setting ItemsSource to a DataGridCollectionView breaks the data binding of the columns.
		DataGridCollectionView transactionsView = new(this.ViewModel!.Transactions);
		transactionsView.SortDescriptions.Add(DataGridSortDescription.FromComparer(TransactionChronologicalComparer.NewestToOldest));
		this.HistoryGrid.ItemsSource = transactionsView;
	}
}
