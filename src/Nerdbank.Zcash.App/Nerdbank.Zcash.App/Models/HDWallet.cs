// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Models;

/// <summary>
/// A wallet for all the accounts created from a single seed phrase.
/// </summary>
[MessagePackObject]
public class HDWallet : IPersistableDataHelper
{
	internal static readonly HDWallet DesignTimeWallet = new(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits)) { BirthdayHeight = AppUtilities.SaplingActivationHeight, Name = "Design Time Wallet" };

	private string name = string.Empty;
	private bool isDirty;
	private bool isBackedUp;

	[SerializationConstructor]
	public HDWallet(Bip39Mnemonic mnemonic)
	{
		this.MainNet = new Zip32HDWallet(mnemonic, ZcashNetwork.MainNet);
		this.TestNet = new Zip32HDWallet(mnemonic, ZcashNetwork.TestNet);

		this.MarkSelfDirtyOnPropertyChanged();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	[IgnoreMember]
	public Zip32HDWallet MainNet { get; }

	[IgnoreMember]
	public Zip32HDWallet TestNet { get; }

	[IgnoreMember]
	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

	[Key(0)]
	public Bip39Mnemonic Mnemonic => this.MainNet.Mnemonic!;

	/// <summary>
	/// Gets the birthday height for the mnemonic.
	/// </summary>
	[Key(1)]
	public required ulong BirthdayHeight { get; init; }

	/// <summary>
	/// Gets or sets an optional name for an HD wallet.
	/// </summary>
	/// <remarks>
	/// HD wallets should have names when there are more than one of them so they can be grouped together in the UI
	/// and the user can understand the groupings.
	/// </remarks>
	[Key(2)]
	public required string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	[Key(3)]
	public bool IsBackedUp
	{
		get => this.isBackedUp;
		set => this.RaiseAndSetIfChanged(ref this.isBackedUp, value);
	}

	public Zip32HDWallet GetZip32HDWalletByNetwork(ZcashNetwork network) => network switch
	{
		ZcashNetwork.MainNet => this.MainNet,
		ZcashNetwork.TestNet => this.TestNet,
		_ => throw new ArgumentException(),
	};

	void IPersistableDataHelper.OnPropertyChanged(string propertyName) => this.OnPropertyChanged(propertyName);

	void IPersistableDataHelper.ClearDirtyFlagOnMembers()
	{
	}

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
