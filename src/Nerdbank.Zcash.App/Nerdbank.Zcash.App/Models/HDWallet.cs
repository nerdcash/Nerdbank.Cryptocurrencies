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
	private readonly SortedDictionary<uint, ZcashAccount> accounts = new();
	private bool isSeedPhraseBackedUp;

	public HDWallet(Zip32HDWallet zip32)
	{
		this.Zip32 = zip32;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

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

	public IReadOnlyDictionary<uint, ZcashAccount> Accounts => this.accounts;

	public uint MaxAccountIndex => this.Accounts.Count > 0 ? this.Accounts.Keys.Max() : 0;

	public void AddAccount(ZcashAccount account)
	{
		Requires.Argument(this.Zip32.Equals(account.HDDerivation?.Wallet), nameof(account), "This account does not belong to this HD wallet.");
		if (this.Accounts.ContainsKey(account.HDDerivation.Value.AccountIndex))
		{
			throw new ArgumentException("An account with this index already exists.", nameof(account));
		}

		this.accounts.Add(account.HDDerivation.Value.AccountIndex, account);
	}

	public ZcashAccount AddAccount(uint index)
	{
		if (this.Accounts.ContainsKey(index))
		{
			throw new ArgumentException("An account with this index already exists.", nameof(index));
		}

		ZcashAccount account = new(this.Zip32, index);
		this.accounts.Add(index, account);
		return account;
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
