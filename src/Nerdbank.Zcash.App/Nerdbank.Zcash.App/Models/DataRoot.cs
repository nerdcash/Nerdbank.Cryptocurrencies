// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Nerdbank.Zcash.App.Models;

[MessagePackObject(true)]
public class DataRoot : IPersistableData
{
	private bool isDirty;

	public DataRoot()
	{
		this.StartWatchingForDirtyChild(this.Wallet);
		this.StartWatchingForDirtyChild(this.ContactManager);
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
		set
		{
			if (!value)
			{
				this.Wallet.IsDirty = false;
				this.ContactManager.IsDirty = false;
			}

			this.isDirty = value;
		}
	}

	public ZcashWallet Wallet { get; set; } = new();

	public ContactManager ContactManager { get; set; } = new();

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
