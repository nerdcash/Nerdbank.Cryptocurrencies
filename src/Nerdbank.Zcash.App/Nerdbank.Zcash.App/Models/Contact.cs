// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Models;

[MessagePackFormatter(typeof(Formatter))]
public class Contact : ReactiveObject, IPersistableData
{
	private string name = string.Empty;
	private ZcashAddress? receivingAddress;
	private bool isDirty;

	public Contact()
	{
		this.MarkSelfDirtyOnPropertyChanged();
	}

	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	public ZcashAddress? ReceivingAddress
	{
		get => this.receivingAddress;
		set => this.RaiseAndSetIfChanged(ref this.receivingAddress, value);
	}

	/// <summary>
	/// Gets the addresses that have been assigned to this contact for sending to the wallet owner.
	/// </summary>
	/// <remarks>
	/// The assignments are unique for each account, because each account has its own transparent addresses
	/// and not all diversifiers are valid for sapling, so each account needs to pick its own.
	/// Finally, users on the contact list are only shown an address from a particular account,
	/// so it works out to record the address they saw on a per-account basis.
	/// </remarks>
	public Dictionary<ZcashAccount, AssignedSendingAddresses> AssignedAddresses { get; } = new();

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
	}

	public AssignedSendingAddresses GetOrCreateSendingAddressAssignment(ZcashAccount account)
	{
		if (!this.AssignedAddresses.TryGetValue(account, out AssignedSendingAddresses? assignment))
		{
			DiversifierIndex diversifierIndex = new(DateTime.UtcNow.Ticks);

			// Sapling may force an adjustment to the diversifier index,
			// so let that happen before we record the assignment.
			account.GetDiversifiedAddress(ref diversifierIndex);
			assignment = new AssignedSendingAddresses()
			{
				AssignedDiversifier = diversifierIndex,
			};
			this.AssignedAddresses.Add(account, assignment);
			this.IsDirty = true;
		}

		return assignment;
	}

	public class AssignedSendingAddresses
	{
		public required DiversifierIndex AssignedDiversifier { get; set; }

		public uint? AssignedTransparentAddressIndex { get; set; }
	}

	private class Formatter : IMessagePackFormatter<Contact>
	{
		public Contact Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			options.Security.DepthStep(ref reader);

			string name = string.Empty;
			ZcashAddress? receivingAddress = null;

			int length = reader.ReadArrayHeader();
			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						name = reader.ReadString() ?? string.Empty;
						break;
					case 1:
						if (reader.ReadString() is string address)
						{
							receivingAddress = ZcashAddress.Decode(address);
						}

						break;
					default:
						reader.Skip();
						break;
				}
			}

			reader.Depth--;

			return new()
			{
				Name = name,
				ReceivingAddress = receivingAddress,
				IsDirty = false,
			};
		}

		public void Serialize(ref MessagePackWriter writer, Contact value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(2);
			writer.Write(value.Name);
			writer.Write(value.ReceivingAddress);
		}
	}
}
