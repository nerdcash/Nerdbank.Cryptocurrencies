// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Models;

[MessagePackFormatter(typeof(Formatter))]
public class ContactManager : IContactManager, IPersistableData
{
	private readonly ObservableCollection<Contact> contacts;
	private bool isDirty;

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
		set
		{
			if (this.isDirty != value)
			{
				if (!value)
				{
					foreach (Contact contact in this.contacts)
					{
						contact.IsDirty = false;
					}
				}

				this.isDirty = value;
				this.OnPropertyChanged();
			}
		}
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

			return new(contacts);
		}

		public void Serialize(ref MessagePackWriter writer, ContactManager value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(1);
			options.Resolver.GetFormatterWithVerify<IReadOnlyList<Contact>>().Serialize(ref writer, value.Contacts, options);
		}
	}
}
