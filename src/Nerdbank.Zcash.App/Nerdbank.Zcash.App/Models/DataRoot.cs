// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Nerdbank.Zcash.App.Models;

[MessagePackObject]
public class DataRoot : ITopLevelPersistableData<DataRoot>, IPersistableDataHelper
{
	[IgnoreMember]
	private ZcashWallet wallet;

	[IgnoreMember]
	private ContactManager contactManager;

	[IgnoreMember]
	private bool isDirty;

	public DataRoot()
	{
		this.StartWatchingForDirtyChild(this.wallet = new());
		this.StartWatchingForDirtyChild(this.contactManager = new());
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// Gets a new <see cref="MessagePackSerializerOptions"/> instance that can serialize and deserialize this type and all referenced types.
	/// </summary>
	/// <remarks>
	/// The returned object is mutable and is in fact expected to mutate as the serialization/deserialization process progresses.
	/// It is therefore imperative that each serialization/deserialization operation use a fresh instance of this object.
	/// </remarks>
	public static MessagePackSerializerOptions SerializerOptions => new AppSerializerOptions(
		MessagePackSerializerOptions.Standard.WithResolver(
			CompositeResolver.Create(
				new IMessagePackFormatter[]
				{
					Zip32HDWalletFormatter.Instance,
				},
				new IFormatterResolver[]
				{
					StandardResolverAllowPrivate.Instance,
				})));

	[IgnoreMember]
	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

	[Key("wallet")]
	public ZcashWallet Wallet
	{
		get => this.wallet;
		set
		{
			this.wallet = value;
			this.StartWatchingForDirtyChild(value);
		}
	}

	[Key("contactManager")]
	public ContactManager ContactManager
	{
		get => this.contactManager;
		set
		{
			this.contactManager = value;
			this.StartWatchingForDirtyChild(value);
		}
	}

	public static DataRoot Load(Stream stream) => MessagePackSerializer.Deserialize<DataRoot>(stream, SerializerOptions);

	public Task SaveAsync(Stream stream, CancellationToken cancellationToken) => MessagePackSerializer.SerializeAsync(stream, this, SerializerOptions, cancellationToken);

	void IPersistableDataHelper.OnPropertyChanged(string propertyName) => this.OnPropertyChanged(propertyName);

	void IPersistableDataHelper.ClearDirtyFlagOnMembers()
	{
		this.Wallet.IsDirty = false;
		this.ContactManager.IsDirty = false;
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
