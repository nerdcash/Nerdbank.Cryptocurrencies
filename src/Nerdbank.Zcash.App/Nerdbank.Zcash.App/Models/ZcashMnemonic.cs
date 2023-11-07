// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Models;

[MessagePackFormatter(typeof(Formatter))]
public class ZcashMnemonic : IPersistableDataHelper
{
	private bool isBackedUp;
	private bool isDirty;

	public ZcashMnemonic(string? password = null)
		: this(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits, password))
	{
	}

	public ZcashMnemonic(Bip39Mnemonic mnemonic)
	{
		this.Bip39 = mnemonic;
		this.MarkSelfDirtyOnPropertyChanged();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public bool IsBackedUp
	{
		get => this.isBackedUp;
		set => this.RaiseAndSetIfChanged(ref this.isBackedUp, value);
	}

	/// <summary>
	/// Gets the birthday height for the mnemonic.
	/// </summary>
	public ulong BirthdayHeight { get; init; } = AppUtilities.SaplingActivationHeight;

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

	public Bip39Mnemonic Bip39 { get; }

	public void ClearDirtyFlagOnMembers()
	{
	}

	void IPersistableDataHelper.OnPropertyChanged(string propertyName) => this.OnPropertyChanged(propertyName);

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	protected void RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(field, value))
		{
			field = value;
			this.OnPropertyChanged(propertyName);
		}
	}

	private class Formatter : IMessagePackFormatter<ZcashMnemonic>
	{
		public ZcashMnemonic Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			options.Security.DepthStep(ref reader);

			int length = reader.ReadArrayHeader();

			Bip39Mnemonic? mnemonic = null;
			ulong? height = null;
			bool? backedUp = null;
			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						mnemonic = Bip39Mnemonic.Parse(options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options));
						break;
					case 1:
						backedUp = reader.ReadBoolean();
						break;
					case 2:
						height = reader.ReadUInt64();
						break;
					default:
						reader.Skip();
						break;
				}
			}

			reader.Depth--;

			return new ZcashMnemonic(mnemonic ?? throw new MessagePackSerializationException("Invalid mnemonic data."))
			{
				IsBackedUp = backedUp ?? false,
				BirthdayHeight = height ?? AppUtilities.SaplingActivationHeight,
				IsDirty = false,
			};
		}

		public void Serialize(ref MessagePackWriter writer, ZcashMnemonic value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(3);
			options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Bip39.SeedPhrase, options);
			writer.Write(value.IsBackedUp);
			writer.Write(value.BirthdayHeight);
		}
	}
}
