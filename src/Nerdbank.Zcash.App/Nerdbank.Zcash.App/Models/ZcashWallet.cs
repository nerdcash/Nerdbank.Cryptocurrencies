// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nerdbank.Zcash.App.Models;

/// <summary>
/// A wallet that contains all the accounts the user wants to track.
/// </summary>
public class ZcashWallet : INotifyPropertyChanged, IEnumerable<ZcashAccount>
{
	private readonly ObservableCollection<HDWallet> hdWallets = new();
	private readonly ObservableCollection<ZcashAccount> loneAccounts = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashWallet"/> class.
	/// </summary>
	public ZcashWallet()
	{
		this.hdWallets.CollectionChanged += (_, _) => this.OnPropertyChanged(nameof(this.IsEmpty));
		this.loneAccounts.CollectionChanged += (_, _) => this.OnPropertyChanged(nameof(this.IsEmpty));
	}

	/// <summary>
	/// Occurs when a property value changes.
	/// </summary>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// Gets a value indicating whether the wallet has no lone accounts and no HD wallets (whether or not they are empty).
	/// </summary>
	public bool IsEmpty => this.HDWallets.Count == 0 && this.LoneAccounts.Count == 0;

	/// <summary>
	/// Gets a collection of all the seed phrase based wallets, which in turn contain accounts created within them.
	/// </summary>
	public IReadOnlyList<HDWallet> HDWallets => this.hdWallets;

	/// <summary>
	/// Gets a collection of the accounts that were imported without a seed phrase.
	/// </summary>
	public IReadOnlyList<ZcashAccount> LoneAccounts => this.loneAccounts;

	/// <summary>
	/// Gets a sequence of all accounts in the wallet.
	/// </summary>
	public IEnumerable<IGrouping<HDWallet?, ZcashAccount>> AllAccounts =>
		(from hd in this.HDWallets
		 from account in hd.Accounts.Values
		 group account by hd into seedPhrase
		 select seedPhrase).Concat(this.LoneAccounts.GroupBy(a => (HDWallet?)null));

	/// <summary>
	/// Adds an account to the wallet.
	/// </summary>
	/// <param name="account">The account to add.</param>
	public void Add(ZcashAccount account)
	{
		if (account.HDDerivation is { } derivation)
		{
			HDWallet? hd = this.HDWallets.FirstOrDefault(w => w.Zip32.Equals(derivation.Wallet));
			if (hd is null)
			{
				hd = new HDWallet(derivation.Wallet);
				this.hdWallets.Add(hd);
			}

			hd.AddAccount(account);
		}
		else
		{
			this.loneAccounts.Add(account);
		}
	}

	/// <inheritdoc/>
	public IEnumerator<ZcashAccount> GetEnumerator() => this.HDWallets.SelectMany(hd => hd.Accounts.Values).Concat(this.LoneAccounts).GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	/// <summary>
	/// Gets the <see cref="HDWallet"/> that contains the given account.
	/// </summary>
	/// <param name="account">The account to search with.</param>
	/// <returns>The <see cref="HDWallet"/> that contains the given <paramref name="account"/>; or <see langword="null" /> if none was found.</returns>
	internal HDWallet? GetHDWalletFor(ZcashAccount account) => this.HDWallets.FirstOrDefault(w => w.Accounts.Values.Contains(account));

	/// <summary>
	/// Raises the <see cref="PropertyChanged"/> event.
	/// </summary>
	/// <param name="propertyName">The name of the property that was changed.</param>
	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
