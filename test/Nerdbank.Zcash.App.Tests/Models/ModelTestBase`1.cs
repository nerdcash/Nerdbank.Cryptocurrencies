// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;

namespace Models;

public abstract class ModelTestBase<T>
	where T : IPersistableData
{
	public ModelTestBase(ITestOutputHelper logger)
	{
		this.Logger = logger;
	}

	public abstract T Model { get; }

	protected static Bip39Mnemonic Mnemonic { get; } = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits);

	protected ITestOutputHelper Logger { get; }

	[Fact]
	public void Serialize_Deserialize_Empty()
	{
		this.SerializeRoundtrip();
	}

	[Fact]
	public void IsDirty_Default()
	{
		Assert.False(this.Model.IsDirty);
	}

	protected T SerializeRoundtrip()
	{
		byte[] buffer = MessagePackSerializer.Serialize(this.Model, DataRoot.SerializerOptions);
		this.Model.IsDirty = false;
		this.Logger.WriteLine($"Data saved in {buffer.Length} bytes.");
		this.Logger.WriteLine(MessagePackSerializer.ConvertToJson(buffer, MessagePackSerializerOptions.Standard));
		T deserialized = MessagePackSerializer.Deserialize<T>(buffer, DataRoot.SerializerOptions);
		Assert.False(deserialized.IsDirty);
		return deserialized;
	}
}
