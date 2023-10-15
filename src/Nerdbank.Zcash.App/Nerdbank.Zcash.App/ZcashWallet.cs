// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App;

public class ZcashWallet : INotifyPropertyChanged
{
	private bool isSeedPhraseBackedUp;

	public ZcashWallet()
	{
		this.isSeedPhraseBackedUp = false;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public bool IsSeedPhraseBackedUp
	{
		get => this.isSeedPhraseBackedUp;
		set => this.RaiseAndSetIfChanged(ref this.isSeedPhraseBackedUp, value);
	}

	public Bip39Mnemonic Mnemonic { get; init; } = Bip39Mnemonic.Create(128);

	public SortedDictionary<uint, ZcashAccount> Accounts { get; } = new();

	public uint? MaxAccountIndex => this.Accounts.Count > 0 ? this.Accounts.Keys.Max() : null;

	public uint? MaxTransparentAddressIndex { get; set; }

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
