// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

/// <summary>
/// Sorts transactions chronologically.
/// </summary>
public class TransactionChronologicalComparer :
	IComparer<TransactionViewModel>,
	IComparer<ZcashTransaction>,
	IOptimizedComparer<TransactionViewModel>,
	IOptimizedComparer<ZcashTransaction>,
	System.Collections.IComparer
{
	private readonly bool oldestToNewest;

	private TransactionChronologicalComparer(bool oldestToNewest)
	{
		this.oldestToNewest = oldestToNewest;
	}

	public static TransactionChronologicalComparer OldestToNewest { get; } = new(true);

	public static TransactionChronologicalComparer NewestToOldest { get; } = new(false);

	public int Compare(TransactionViewModel? x, TransactionViewModel? y) => this.Compare(x?.Model, y?.Model);

	public int Compare(ZcashTransaction? x, ZcashTransaction? y)
	{
		int result = CompareOldestToNewest(x, y);
		return this.oldestToNewest ? result : -result;

		static int CompareOldestToNewest(ZcashTransaction? x, ZcashTransaction? y)
		{
			if (x is null)
			{
				return y is null ? 0 : -1;
			}

			if (y is null)
			{
				return 1;
			}

			// Block number is always the 1st order sort.
			if (x.BlockNumber.HasValue && y.BlockNumber.HasValue)
			{
				int blockNumberComparison = x.BlockNumber.Value.CompareTo(y.BlockNumber.Value);
				if (blockNumberComparison != 0)
				{
					return blockNumberComparison;
				}
			}
			else if (x.BlockNumber.HasValue)
			{
				return -1;
			}
			else if (y.BlockNumber.HasValue)
			{
				return 1;
			}

			return (x.When ?? DateTimeOffset.MaxValue).CompareTo(y.When ?? DateTimeOffset.MaxValue);
		}
	}

	bool IOptimizedComparer<TransactionViewModel>.IsPropertySignificant(string propertyName) => propertyName is nameof(TransactionViewModel.When) or nameof(TransactionViewModel.BlockNumber);

	bool IOptimizedComparer<ZcashTransaction>.IsPropertySignificant(string propertyName) => propertyName is nameof(ZcashTransaction.When) or nameof(ZcashTransaction.BlockNumber);

	int System.Collections.IComparer.Compare(object? x, object? y)
	{
		return x is TransactionViewModel || y is TransactionViewModel
			? this.Compare((TransactionViewModel?)x, (TransactionViewModel?)y)
			: this.Compare((ZcashTransaction?)x, (ZcashTransaction?)y);
	}
}
