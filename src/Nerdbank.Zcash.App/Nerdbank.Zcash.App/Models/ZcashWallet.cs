// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
public class ZcashWallet : INotifyPropertyChanged, IPersistableDataHelper
{
	private readonly ObservableCollection<HDWallet> hdWallets;
	private readonly ObservableCollection<Account> accounts;
	private bool isDirty;

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashWallet"/> class.
	/// </summary>
	public ZcashWallet()
		: this(Array.Empty<HDWallet>(), Array.Empty<Account>())
	{
	}

	private ZcashWallet(IReadOnlyList<HDWallet> hdWallets, IReadOnlyList<Account> accounts)
	{
		this.hdWallets = new(hdWallets);
		this.HDWallets = new(this.hdWallets);

		this.accounts = new(accounts);
		this.Accounts = new(this.accounts);

		this.accounts.CollectionChanged += (_, _) => this.OnPropertyChanged(nameof(this.IsEmpty));

		this.StartWatchingForDirtyChildren(this.hdWallets);

		this.hdWallets.NotifyOnCollectionElementMemberChanged(nameof(HDWallet.IsSeedPhraseBackedUp), (HDWallet? hd) => this.OnPropertyChanged(nameof(this.HDWalletsRequireBackup)));
	}

	/// <summary>
	/// Occurs when a property value changes.
	/// </summary>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// Occurs when an account is added or removed from the wallet.
	/// </summary>
	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

	public bool HDWalletsRequireBackup => this.HDWallets.Any(w => !w.IsSeedPhraseBackedUp);

	/// <summary>
	/// Gets a value indicating whether the wallet has no lone accounts and no HD wallets (whether or not they are empty).
	/// </summary>
	public bool IsEmpty => this.Accounts.Count == 0;

	/// <summary>
	/// Gets a collection of all the seed phrase based wallets, which in turn contain accounts created within them.
	/// </summary>
	public ReadOnlyObservableCollection<HDWallet> HDWallets { get; }

	/// <summary>
	/// Gets the collection of all accounts in the wallet.
	/// </summary>
	public ReadOnlyObservableCollection<Account> Accounts { get; }

	public bool TryGetHDWallet(Account account, [NotNullWhen(true)] out HDWallet? wallet) => this.TryGetHDWallet(account.ZcashAccount, out wallet);

	public bool TryGetHDWallet(ZcashAccount account, [NotNullWhen(true)] out HDWallet? wallet)
	{
		if (account.HDDerivation?.Wallet is Zip32HDWallet zip32)
		{
			wallet = this.HDWallets.FirstOrDefault(w => w.Zip32.Equals(zip32));
			return wallet is not null;
		}

		wallet = null;
		return false;
	}

	public IEnumerable<Account> GetAccountsUnder(HDWallet hd) => this.Accounts.Where(a => a.ZcashAccount.HDDerivation?.Wallet.Equals(hd.Zip32) is true);

	public uint GetMaxAccountIndex(HDWallet hd) => this.GetAccountsUnder(hd).Max(a => a.ZcashAccount.HDDerivation!.Value.AccountIndex);

	/// <summary>
	/// Adds an account to the wallet.
	/// </summary>
	/// <param name="account">The account to add.</param>
	/// <returns>The account model wrapper that is created to wrap the <see cref="ZcashAccount"/>.</returns>
	public Account Add(ZcashAccount account)
	{
		Account accountModel = new(account);
		this.Add(accountModel);
		return accountModel;
	}

	public void Add(Account account)
	{
		if (account.ZcashAccount.HDDerivation is { } derivation && !this.TryGetHDWallet(account, out HDWallet? hd))
		{
			hd = new HDWallet(derivation.Wallet);
			this.hdWallets.Add(hd);
			this.StartWatchingForDirtyChild(hd);
		}

		this.accounts.Add(account);
		this.StartWatchingForDirtyChild(account);
	}

	public Account Add(HDWallet hd, uint index) => this.Add(new ZcashAccount(hd.Zip32, index));

	public bool Remove(Account account, IContactManager? contactManager)
	{
		if (this.accounts.Remove(account))
		{
			if (contactManager is not null)
			{
				this.ScrubAccountReferenceFromContacts(account, contactManager);
			}

			return true;
		}

		return false;
	}

	void IPersistableDataHelper.OnPropertyChanged(string propertyName) => this.OnPropertyChanged(propertyName);

	void IPersistableDataHelper.ClearDirtyFlagOnMembers()
	{
		this.HDWallets.ClearDirtyFlag();
		this.Accounts.ClearDirtyFlag();
	}

	/// <summary>
	/// Raises the <see cref="PropertyChanged"/> event.
	/// </summary>
	/// <param name="propertyName">The name of the property that was changed.</param>
	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	/// <summary>
	/// Raises the <see cref="CollectionChanged"/> event.
	/// </summary>
	/// <param name="e">The changed event, which should describe <see cref="Account"/> objects.</param>
	protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) => this.CollectionChanged?.Invoke(this, e);

	private void ScrubAccountReferenceFromContacts(Account account, IContactManager contactManager)
	{
		// Enumerate contacts and remove any record of them observing a receiving address from the removed account.
		if (contactManager is not null)
		{
			foreach (Contact contact in contactManager.Contacts)
			{
				contact.RemoveSendingAddressAssignment(account);
			}
		}
	}

	private class Formatter : IMessagePackFormatter<ZcashWallet>
	{
		public ZcashWallet Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			HDWallet[] hdWallets = Array.Empty<HDWallet>();
			Account[] accounts = Array.Empty<Account>();

			int length = reader.ReadArrayHeader();
			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						hdWallets = options.Resolver.GetFormatterWithVerify<HDWallet[]>().Deserialize(ref reader, options);
						break;
					case 1:
						accounts = options.Resolver.GetFormatterWithVerify<Account[]>().Deserialize(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			return new(hdWallets, accounts);
		}

		public void Serialize(ref MessagePackWriter writer, ZcashWallet value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(2);
			options.Resolver.GetFormatterWithVerify<IReadOnlyList<HDWallet>>().Serialize(ref writer, value.HDWallets, options);
			options.Resolver.GetFormatterWithVerify<IReadOnlyList<Account>>().Serialize(ref writer, value.Accounts, options);
		}
	}
}
