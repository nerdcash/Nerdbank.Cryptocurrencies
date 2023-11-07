// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;
using Microsoft;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.Models;

/// <summary>
/// A wallet for all the accounts created from a single seed phrase.
/// </summary>
[MessagePackFormatter(typeof(Formatter))]
public class HDWallet : IPersistableDataHelper
{
	private string name = string.Empty;
	private bool isDirty;

	public HDWallet(Zip32HDWallet zip32)
	{
		this.Zip32 = zip32;
		this.MarkSelfDirtyOnPropertyChanged();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

	/// <summary>
	/// Gets or sets an optional name for an HD wallet.
	/// </summary>
	/// <remarks>
	/// HD wallets should have names when there are more than one of them so they can be grouped together in the UI
	/// and the user can understand the groupings.
	/// </remarks>
	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	public Zip32HDWallet Zip32 { get; }

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

	private class Formatter : IMessagePackFormatter<HDWallet>
	{
		public HDWallet Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			options.Security.DepthStep(ref reader);

			HDWallet? wallet = null;

			int length = reader.ReadArrayHeader();
			if (length < 1)
			{
				throw new MessagePackSerializationException("Invalid HD wallet data.");
			}

			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						Zip32HDWallet zip32 = options.Resolver.GetFormatterWithVerify<Zip32HDWallet>().Deserialize(ref reader, options);
						wallet = new HDWallet(zip32);
						break;
					case 1:
						wallet!.Name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options) ?? string.Empty;
						break;
					default:
						reader.Skip();
						break;
				}
			}

			reader.Depth--;

			wallet!.IsDirty = false;
			return wallet;
		}

		public void Serialize(ref MessagePackWriter writer, HDWallet value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(2);

			options.Resolver.GetFormatterWithVerify<Zip32HDWallet>().Serialize(ref writer, value.Zip32, options);
			options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
		}
	}
}
