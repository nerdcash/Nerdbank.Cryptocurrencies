// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.Models;

/// <summary>
/// A wallet for all the accounts created from a single seed phrase.
/// </summary>
public class HDWallet : INotifyPropertyChanged
{
	private readonly SortedDictionary<uint, Account> accounts = new();
	private bool isSeedPhraseBackedUp;
	private string name = string.Empty;

	public HDWallet(Zip32HDWallet zip32)
	{
		this.Zip32 = zip32;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// Gets or sets an optional name for an HD wallet.
	/// </summary>
	/// <remarks>
	/// HD wallets should have names when there are more than one of them so they can be grouped together in the UI
	/// and the user can understand the groupings.
	/// </remarks>
	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	public bool IsSeedPhraseBackedUp
	{
		get => this.isSeedPhraseBackedUp;
		set => this.RaiseAndSetIfChanged(ref this.isSeedPhraseBackedUp, value);
	}

	public Zip32HDWallet Zip32 { get; }

	/// <summary>
	/// Gets or sets the birthday height for the overall HD wallet.
	/// </summary>
	public ulong BirthdayHeight { get; set; }

	public IReadOnlyDictionary<uint, Account> Accounts => this.accounts;

	public uint MaxAccountIndex => this.Accounts.Count > 0 ? this.Accounts.Keys.Max() : 0;

	public Account AddAccount(ZcashAccount account)
	{
		Account accountModel = new(account, this);
		this.AddAccount(accountModel);
		return accountModel;
	}

	public void AddAccount(Account account)
	{
		Requires.Argument(account.MemberOf == this, nameof(account), "This account does not belong to this HD wallet.");
		Requires.Argument(this.Zip32.Equals(account.ZcashAccount.HDDerivation?.Wallet), nameof(account), "This account does not belong to this HD wallet.");
		if (this.Accounts.ContainsKey(account.ZcashAccount.HDDerivation.Value.AccountIndex))
		{
			throw new ArgumentException("An account with this index already exists.", nameof(account));
		}

		this.accounts.Add(account.ZcashAccount.HDDerivation.Value.AccountIndex, account);
	}

	public Account AddAccount(uint index)
	{
		if (this.Accounts.ContainsKey(index))
		{
			throw new ArgumentException("An account with this index already exists.", nameof(index));
		}

		Account accountModel = new(new ZcashAccount(this.Zip32, index), this);
		this.accounts.Add(index, accountModel);
		return accountModel;
	}

	public bool RemoveAccount(uint index) => this.accounts.Remove(index);

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	protected void RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(field, value))
		{
			field = value;
			this.OnPropertyChanged(propertyName);
		}
	}
}
