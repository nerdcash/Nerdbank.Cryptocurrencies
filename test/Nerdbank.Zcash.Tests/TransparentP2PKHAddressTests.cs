// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TransparentP2PKHAddressTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public TransparentP2PKHAddressTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void Ctor_Receiver()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TransparentP2PKHReceiver receiver = new(hash);
		TransparentP2PKHAddress addr = new(receiver, ZcashNetwork.MainNet);
		Assert.Equal("t1HseQJEmpT7jcnTGoJVsKg5fuTzhfNXu9v", addr.Address);
		Assert.Equal(ZcashNetwork.MainNet, addr.Network);
	}

	[Fact]
	public void Ctor_Receiver_TestNet()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TransparentP2PKHReceiver receiver = new(hash);
		TransparentP2PKHAddress addr = new(receiver, ZcashNetwork.TestNet);
		Assert.Equal("tm9iPj8jBD7dEm2eiU2ocBLkRWT5XBEXDQA", addr.Address);
		Assert.Equal(ZcashNetwork.TestNet, addr.Network);
	}

	[Fact]
	public void GetPoolReceiver()
	{
		Assert.NotNull(ZcashAddress.Decode(ValidTransparentP2PKHAddress).GetPoolReceiver<TransparentP2PKHReceiver>());
		Assert.Null(ZcashAddress.Decode(ValidTransparentP2PKHAddress).GetPoolReceiver<TransparentP2SHReceiver>());
		Assert.Null(ZcashAddress.Decode(ValidTransparentP2PKHAddress).GetPoolReceiver<SaplingReceiver>());
	}

	[Fact]
	public void AddressDerivation()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Parse("diary slender airport");
		using Bip32HDWallet.ExtendedPrivateKey masterKey = Bip32HDWallet.ExtendedPrivateKey.Create(mnemonic, testNet: true);
		using Bip32HDWallet.ExtendedPrivateKey accountKey = masterKey.Derive(Bip44MultiAccountHD.CreateKeyPath(133, 0));
		Bip32HDWallet.ExtendedPublicKey accountPublicKey = accountKey.PublicKey;

		string[] expectedAddresses = new[]
		{
			"t1KYzFvwG5f1ZgiR3oeBuWU9BLTPcwr4zPe",
			"t1V8GgZ8yf3nVXvkQwdvGN5fo6EPE6CCrAQ",
		};

		Bip32HDWallet.KeyPath externalChain = new((uint)Bip44MultiAccountHD.Change.ReceivingAddressChain);
		for (uint i = 0; i < expectedAddresses.Length; i++)
		{
			Bip32HDWallet.KeyPath addrKeyPath = new(i, externalChain);
			this.logger.WriteLine($"{new Bip32HDWallet.KeyPath(i, addrKeyPath)}");
			Bip32HDWallet.ExtendedPublicKey addrExtendedPublicKey = accountPublicKey.Derive(addrKeyPath);

			TransparentP2PKHReceiver receiver = new(new Zip32HDWallet.Transparent.ExtendedViewingKey(addrExtendedPublicKey, ZcashNetwork.TestNet));
			TransparentP2PKHAddress addr = new(receiver);

			this.logger.WriteLine($"{addr}");
			Assert.Equal(expectedAddresses[i], addr.Address);
		}
	}
}
