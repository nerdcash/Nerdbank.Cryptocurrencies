﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Nerdbank.Zcash.App.Models;

[MessagePackFormatter(typeof(Formatter))]
public class Contact : ReactiveObject, IPersistableData
{
	private readonly ImmutableDictionary<Account, AssignedSendingAddresses>.Builder assignedAddresses;
	private readonly ObservableCollection<ZcashAddress> receivingAddresses;
	private string name = string.Empty;
	private bool isDirty = true;

	public Contact()
		: this(ImmutableDictionary<Account, AssignedSendingAddresses>.Empty, new ObservableCollection<ZcashAddress>())
	{
		this.MarkSelfDirtyOnPropertyChanged();
	}

	private Contact(ImmutableDictionary<Account, AssignedSendingAddresses> assignedAddresses, ObservableCollection<ZcashAddress> receivingAddresses)
	{
		this.assignedAddresses = assignedAddresses.ToBuilder();
		this.receivingAddresses = receivingAddresses;
	}

	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	/// <summary>
	/// Gets the collection of receiving addresses we know of for this contact.
	/// </summary>
	public ObservableCollection<ZcashAddress> ReceivingAddresses => this.receivingAddresses;

	/// <summary>
	/// Gets the addresses that have been assigned to this contact for sending to the wallet owner.
	/// </summary>
	/// <remarks>
	/// The assignments are unique for each account, because each account has its own transparent addresses
	/// and not all diversifiers are valid for sapling, so each account needs to pick its own.
	/// Finally, users on the contact list are only shown an address from a particular account,
	/// so it works out to record the address they saw on a per-account basis.
	/// </remarks>
	public ImmutableDictionary<Account, AssignedSendingAddresses> AssignedAddresses => this.assignedAddresses.ToImmutable();

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
	}

	public AssignedSendingAddresses GetOrCreateSendingAddressAssignment(Account account)
	{
		if (!this.AssignedAddresses.TryGetValue(account, out AssignedSendingAddresses? assignment))
		{
			DiversifierIndex diversifierIndex = new(DateTime.UtcNow.Ticks);

			// Sapling may force an adjustment to the diversifier index,
			// so let that happen before we record the assignment.
			account.ZcashAccount.GetDiversifiedAddress(ref diversifierIndex);
			assignment = new AssignedSendingAddresses()
			{
				Diversifier = diversifierIndex,
			};
			this.assignedAddresses.Add(account, assignment);
			this.RaisePropertyChanged(nameof(this.AssignedAddresses));
		}

		return assignment;
	}

	public bool RemoveSendingAddressAssignment(Account account)
	{
		if (this.assignedAddresses.Remove(account))
		{
			this.RaisePropertyChanged(nameof(this.AssignedAddresses));
			return true;
		}

		return false;
	}

	public override string ToString() => this.Name;

	/// <summary>
	/// Gets the recommended receiving address to use to send Zcash to this contact.
	/// </summary>
	/// <param name="network">The network to be used to send the funds.</param>
	/// <returns>The address to use, if any applicable address is set on this contact.</returns>
	public ZcashAddress? GetReceivingAddress(ZcashNetwork network) => this.ReceivingAddresses.FirstOrDefault(a => a.Network == network);

	[MessagePackFormatter(typeof(Formatter))]
	public class AssignedSendingAddresses
	{
		public required DiversifierIndex Diversifier { get; set; }

		public uint? TransparentAddressIndex { get; set; }

		private class Formatter : IMessagePackFormatter<AssignedSendingAddresses>
		{
			public AssignedSendingAddresses Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
			{
				options.Security.DepthStep(ref reader);

				int length = reader.ReadArrayHeader();
				if (length < 1)
				{
					throw new MessagePackSerializationException("Expected at least one element in the array.");
				}

				DiversifierIndex diversifierIndex = default;
				uint? transparentIndex = null;

				for (int i = 0; i < length; i++)
				{
					switch (i)
					{
						case 0:
							ReadOnlySequence<byte> diversifierBytes = reader.ReadBytes() ?? throw new MessagePackSerializationException();
							if (diversifierBytes.IsSingleSegment)
							{
								diversifierIndex = new(diversifierBytes.FirstSpan);
							}
							else
							{
								diversifierBytes.CopyTo(diversifierIndex);
							}

							break;
						case 1:
							transparentIndex = reader.ReadUInt32();
							break;
						default:
							reader.Skip();
							break;
					}
				}

				reader.Depth--;

				return new AssignedSendingAddresses
				{
					Diversifier = diversifierIndex,
					TransparentAddressIndex = transparentIndex,
				};
			}

			public void Serialize(ref MessagePackWriter writer, AssignedSendingAddresses value, MessagePackSerializerOptions options)
			{
				writer.WriteArrayHeader(value.TransparentAddressIndex.HasValue ? 2 : 1);

				DiversifierIndex diversifier = value.Diversifier;
				writer.WriteBinHeader(DiversifierIndex.Length);
				diversifier[..].CopyTo(writer.GetSpan(DiversifierIndex.Length));
				writer.Advance(DiversifierIndex.Length);

				if (value.TransparentAddressIndex.HasValue)
				{
					writer.Write(value.TransparentAddressIndex.Value);
				}
			}
		}
	}

	/// <summary>
	/// A custom formatter for <see cref="Contact"/>.
	/// </summary>
	/// <remarks>
	/// This formatter is necessary because we need to initialize a private field, and we don't use <see cref="DynamicObjectResolverAllowPrivate"/>.
	/// </remarks>
	private class Formatter : IMessagePackFormatter<Contact>
	{
		public Contact Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			options.Security.DepthStep(ref reader);

			string name = string.Empty;
			ObservableCollection<ZcashAddress>? receivingAddresses = null;
			ImmutableDictionary<Account, AssignedSendingAddresses> assignedAddresses = ImmutableDictionary<Account, AssignedSendingAddresses>.Empty;

			int length = reader.ReadArrayHeader();
			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options) ?? string.Empty;
						break;
					case 1:
						receivingAddresses = options.Resolver.GetFormatterWithVerify<ObservableCollection<ZcashAddress>>().Deserialize(ref reader, options);
						break;
					case 2:
						assignedAddresses = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<Account, AssignedSendingAddresses>>().Deserialize(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			reader.Depth--;

			return new(assignedAddresses, receivingAddresses ?? new())
			{
				Name = name,
				IsDirty = false,
			};
		}

		public void Serialize(ref MessagePackWriter writer, Contact value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(3);
			options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
			options.Resolver.GetFormatterWithVerify<ObservableCollection<ZcashAddress>>().Serialize(ref writer, value.ReceivingAddresses, options);
			options.Resolver.GetFormatterWithVerify<ImmutableDictionary<Account, AssignedSendingAddresses>>().Serialize(ref writer, value.AssignedAddresses, options);
		}
	}
}
