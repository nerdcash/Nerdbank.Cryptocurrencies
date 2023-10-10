// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using DynamicData;

namespace Nerdbank.Zcash.App.ViewModels;

public class TransactionHistoryViewModel : ViewModelBase
{
	private TransactionViewModel? selectedTransaction;

	[Obsolete("For design-time use only", error: true)]
	public TransactionHistoryViewModel()
	{
		this.CommonConstruction();
		this.Transactions.AddRange(new TransactionViewModel[]
		{
			new() { Amount = 1.2345m, IsIncoming = true, OtherPartyName = "Andrew Arnott", Memo = "For the pizza", TransactionId = "12345abc" },
			new() { Amount = -0.5m, IsIncoming = false, OtherPartyName = "Red Rock Cafe", Memo = "Hot Chocolate", TransactionId = "1e62b7" },
		});
	}

	public string Title => "Transaction History";

	public ObservableCollection<TransactionViewModel> Transactions { get; } = new();

	public string WhenColumnHeader => "When";

	public string AmountColumnHeader => this.Network.GetTickerName();

	public string FiatAmountColumnHeader => "USD";

	public string OtherPartyNameColumnHeader => "Name";

	public string MemoColumnHeader => "Memo";

	public string RunningBalanceColumnHeader => "Balance";

	public TransactionViewModel? SelectedTransaction
	{
		get => this.selectedTransaction;
		set => this.RaiseAndSetIfChanged(ref this.selectedTransaction, value);
	}

	public bool IsTransactionDetailsVisible => this.SelectedTransaction is not null;

	private void CommonConstruction()
	{
		this.LinkProperty(nameof(this.SelectedTransaction), nameof(this.IsTransactionDetailsVisible));
	}
}
