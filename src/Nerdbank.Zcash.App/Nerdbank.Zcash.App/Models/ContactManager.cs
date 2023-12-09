// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Models;

[MessagePackFormatter(typeof(Formatter))]
public class ContactManager : IContactManager, IPersistableDataHelper
{
	private readonly ObservableCollection<Contact> contacts;
	private bool isDirty = true;

	public ContactManager()
		: this(Array.Empty<Contact>())
	{
	}

	private ContactManager(IReadOnlyList<Contact> contacts)
	{
		this.contacts = new(contacts);
		this.Contacts = new(this.contacts);
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	[IgnoreMember]
	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

	public ReadOnlyObservableCollection<Contact> Contacts { get; }

	public void Add(Contact contact)
	{
		if (!this.contacts.Contains(contact))
		{
			this.StartWatchingForDirtyChild(contact);
			this.contacts.Add(contact);
			this.IsDirty = true;
		}
	}

	public bool Remove(Contact contact)
	{
		if (this.contacts.Remove(contact))
		{
			// unsubscribe.
			this.IsDirty = true;
			return true;
		}

		return false;
	}

	void IPersistableDataHelper.OnPropertyChanged(string propertyName) => this.OnPropertyChanged(propertyName);

	void IPersistableDataHelper.ClearDirtyFlagOnMembers()
	{
		this.contacts.ClearDirtyFlag();
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	private class Formatter : IMessagePackFormatter<ContactManager>
	{
		public ContactManager Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			options.Security.DepthStep(ref reader);

			Contact[] contacts = Array.Empty<Contact>();

			int length = reader.ReadArrayHeader();
			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						contacts = options.Resolver.GetFormatterWithVerify<Contact[]>().Deserialize(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			reader.Depth--;

			ContactManager result = new(contacts);
			result.IsDirty = false;
			return result;
		}

		public void Serialize(ref MessagePackWriter writer, ContactManager value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(1);
			options.Resolver.GetFormatterWithVerify<IReadOnlyList<Contact>>().Serialize(ref writer, value.Contacts, options);
		}
	}
}
