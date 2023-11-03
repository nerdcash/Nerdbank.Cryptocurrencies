// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;
using Microsoft;

namespace Nerdbank.Zcash.App.Models;

/// <summary>
/// A wallet that contains all the accounts the user wants to track.
/// </summary>
[MessagePackFormatter(typeof(Formatter))]
public class ZcashWallet : INotifyPropertyChanged, IEnumerable<Account>, IPersistableDataHelper
{
	private readonly ObservableCollection<HDWallet> hdWallets;
	private readonly ObservableCollection<Account> loneAccounts;
	private bool isDirty;

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashWallet"/> class.
	/// </summary>
	public ZcashWallet()
		: this(Array.Empty<HDWallet>(), Array.Empty<Account>())
	{
	}

	private ZcashWallet(IReadOnlyList<HDWallet> hdWallets, IReadOnlyList<Account> loneAccounts)
	{
		this.hdWallets = new(hdWallets);
		this.loneAccounts = new(loneAccounts);

		this.LoneAccounts = new(this.loneAccounts);

		this.hdWallets.CollectionChanged += (_, _) => this.OnPropertyChanged(nameof(this.IsEmpty));
		this.loneAccounts.CollectionChanged += (_, _) => this.OnPropertyChanged(nameof(this.IsEmpty));

		this.StartWatchingForDirtyChildren(this.hdWallets);
		this.StartWatchingForDirtyChildren(this.loneAccounts);
	}

	/// <summary>
	/// Occurs when a property value changes.
	/// </summary>
	public event PropertyChangedEventHandler? PropertyChanged;

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

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
	public ReadOnlyObservableCollection<Account> LoneAccounts { get; }

	/// <summary>
	/// Gets a sequence of all accounts in the wallet.
	/// </summary>
	public IEnumerable<IGrouping<HDWallet?, Account>> AllAccounts =>
		(from hd in this.HDWallets
		 from account in hd.Accounts.Values
		 group account by hd into seedPhrase
		 select seedPhrase).Concat(this.LoneAccounts.GroupBy(a => (HDWallet?)null));

	/// <summary>
	/// Adds an account to the wallet.
	/// </summary>
	/// <param name="account">The account to add.</param>
	/// <returns>The account model wrapper that is created to wrap the <see cref="ZcashAccount"/>.</returns>
	public Account Add(ZcashAccount account)
	{
		if (account.HDDerivation is { } derivation)
		{
			HDWallet? hd = this.HDWallets.FirstOrDefault(w => w.Zip32.Equals(derivation.Wallet));
			if (hd is null)
			{
				hd = new HDWallet(derivation.Wallet);
				this.hdWallets.Add(hd);
				this.StartWatchingForDirtyChild(hd);
			}

			return hd.AddAccount(account);
		}
		else
		{
			Account accountModel = new(account, null);
			this.loneAccounts.Add(accountModel);
			this.StartWatchingForDirtyChild(accountModel);
			return accountModel;
		}
	}

	public void Add(Account account)
	{
		Requires.Argument(account.MemberOf is null == account.ZcashAccount.HDDerivation is null, nameof(account), "Account must be either a lone account or a member of a seed phrase wallet.");

		if (account.MemberOf is not null)
		{
			account.MemberOf.AddAccount(account);
			if (!this.hdWallets.Contains(account.MemberOf))
			{
				this.hdWallets.Add(account.MemberOf);
			}
		}
		else
		{
			this.loneAccounts.Add(account);
		}
	}

	public bool Remove(Account account, IContactManager? contactManager)
	{
		if (account.MemberOf is null)
		{
			if (this.loneAccounts.Remove(account))
			{
				if (contactManager is not null)
				{
					this.ScrubAccountReferenceFromContacts(account.ZcashAccount, contactManager);
				}

				return true;
			}
		}
		else if (account.ZcashAccount.HDDerivation is not null)
		{
			if (account.MemberOf.RemoveAccount(account.ZcashAccount.HDDerivation.Value.AccountIndex))
			{
				if (contactManager is not null)
				{
					this.ScrubAccountReferenceFromContacts(account.ZcashAccount, contactManager);
				}

				return true;
			}
		}

		return false;
	}

	/// <inheritdoc/>
	public IEnumerator<Account> GetEnumerator() => this.HDWallets.SelectMany(hd => hd.Accounts.Values).Concat(this.LoneAccounts).GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	void IPersistableDataHelper.OnPropertyChanged(string propertyName) => this.OnPropertyChanged(propertyName);

	void IPersistableDataHelper.ClearDirtyFlagOnMembers()
	{
		this.HDWallets.ClearDirtyFlag();
		this.LoneAccounts.ClearDirtyFlag();
	}

	/// <summary>
	/// Raises the <see cref="PropertyChanged"/> event.
	/// </summary>
	/// <param name="propertyName">The name of the property that was changed.</param>
	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	private void ScrubAccountReferenceFromContacts(ZcashAccount account, IContactManager contactManager)
	{
		// Enumerate contacts and remove any record of them observing a receiving address from the removed account.
		if (contactManager is not null)
		{
			foreach (Contact contact in contactManager.Contacts)
			{
				contact.AssignedAddresses.Remove(account);
			}
		}
	}

	private class Formatter : IMessagePackFormatter<ZcashWallet>
	{
		public ZcashWallet Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			HDWallet[] hdWallets = Array.Empty<HDWallet>();
			Account[] loneAccounts = Array.Empty<Account>();

			int length = reader.ReadArrayHeader();
			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						hdWallets = options.Resolver.GetFormatterWithVerify<HDWallet[]>().Deserialize(ref reader, options);
						break;
					case 1:
						loneAccounts = options.Resolver.GetFormatterWithVerify<Account[]>().Deserialize(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			return new(hdWallets, loneAccounts);
		}

		public void Serialize(ref MessagePackWriter writer, ZcashWallet value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(2);
			options.Resolver.GetFormatterWithVerify<IReadOnlyList<HDWallet>>().Serialize(ref writer, value.HDWallets, options);
			options.Resolver.GetFormatterWithVerify<IReadOnlyList<Account>>().Serialize(ref writer, value.LoneAccounts, options);
		}
	}
}
